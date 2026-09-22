using ImGuiNET;
using Prismatix.Geometry;
using Prismatix.Math;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Prismatix
{
    //TODO:
    //Fix selection of lamps in outliner

    public class Interface
    {
        #region Vars
        private static IWindow window;
        private static ImGuiController guiController;
        private static GL opengl;
        private static System.Collections.Generic.List<Scene> activeScenes = new System.Collections.Generic.List<Scene>();

        private static IMouse mouse;
        private static bool isViewportHovered = false;
        private static Vector2 lastMousePos;
        private static float scrollDelta = 0.0f;
        private static Math.Vector3 pivot = new Math.Vector3(0, 0, 0);
        private static float camYaw = 0.0f;
        private static float camPitch = 0.2f;
        private static float camRadius = 5.0f;

        private static uint currTextureId = 0;
        private static Scene currScene = null;
        private static int currSceneIndex = 0;
        private static int currObjectIndex = -1;
        private static int currLampIndex = -1;
        private static int currRenderMode = 1;

        private static string[] hdriFiles = new string[0];
        private static int currHdriIndex = 0;

        public enum Tool {None, Move, Rotate, Scale}
        private static Tool selectedTool = Tool.None;
        public enum SelectionType { None, Geometry, Lamp }
        public static int currSelection = (int)SelectionType.None;

        private static Vector2 currentViewportSize = new Vector2(1, 1);
        private static Vector2 currentViewportMin = new Vector2(0, 0);
        private static Vector2 leftClickStartPos = new Vector2(0, 0);

        private static double currDeltaTime = 0.0;
        private static bool showBvh = false;
        private static string activeAxis = "";

        private static System.Threading.Tasks.Task<Image> renderTask = null;
        private static bool isRendering = false;
        private static Math.Vector3 lastCamPos = new Math.Vector3(0, 0, 0);
        private static Math.Vector3 lastCamForward = new Math.Vector3(0, 0, 0);
        private static int lastRenderMode = 1;
        private static bool needsRender = true;
        #endregion

        public static void Main()
        {
            var options = WindowOptions.Default;
            options.Title = "Prismatix Studio";
            options.Size = new Silk.NET.Maths.Vector2D<int>(1920, 1080);
            options.API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.Default,
                new APIVersion(3, 3)
            );

            window = Window.Create(options);

            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;
            window.Closing += OnClose;
            window.Resize += OnResize;

            window.Run();
        }

        private static void CreateNewScene()
        {
            Scene newScene = new Scene();

            newScene.mainCamera = new Camera(
                new Prismatix.Math.Vector3(0, 1, -5),
                new Prismatix.Math.Vector3(0, 0, 1),
                new Prismatix.Math.Vector3(0, 1, 0)
            );

            string defaultCubePath = Path.Combine(projectDirectory, "Geometry", "cube.obj");
            if (File.Exists(defaultCubePath))
            {
                Geometry.Object defaultCube = MeshLib.NewObj(defaultCubePath, new Prismatix.Math.Vector3(0, 0, 0), "Default ", 1.0f);
                defaultCube.material = new Material("Default Material", new Prismatix.Math.Vector3(0.8f, 0.8f, 0.8f), 0.5f, 0.5f);
                newScene.AddObject(defaultCube);
            }
            newScene.AddLamp(new Lamp(new Math.Vector3(2.0f, 4.0f, -3.0f), 50.0f));

            activeScenes.Add(newScene);
            currSceneIndex = activeScenes.Count - 1;
            currObjectIndex = 0;

            pivot = new Math.Vector3(0, 0, 0);
            camYaw = 0.0f;
            camPitch = 0.2f;
            camRadius = 5.0f;
            newScene.mainCamera.RotateTo(pivot);
        }

        private static void OnLoad()
        {
            opengl = GL.GetApi(window);
            var input = window.CreateInput();

            if (input.Mice.Count > 0)
            {
                mouse = input.Mice[0];
                mouse.Scroll += (m, scroll) => { scrollDelta = scroll.Y; };
            }

            //silk.net requires font config as delegate to upload to gpu
            guiController = new ImGuiController(opengl, window, input, () =>
            {
                var io = ImGui.GetIO();
                io.Fonts.Clear();
                io.Fonts.AddFontFromFileTTF(Path.Combine(projectDirectory, "seguisb.ttf"), 24.0f);
            });

            ImGui.LoadIniSettingsFromMemory(string.Empty);
            SwitchToDark();

            Config.Load(Path.Combine(projectDirectory, "Config.json"));

            string hdriPath = Path.Combine(projectDirectory, "HDRIs");
            if (Directory.Exists(hdriPath))
            {
                hdriFiles = Directory.GetFiles(hdriPath, "*.hdr");
                hdriFiles = hdriFiles.
                    Concat( Directory.GetFiles(hdriPath, "*.exr") )
                    .ToArray();
            }

            CreateNewScene();
        }
        private static void OnClose()
        {
            guiController?.Dispose();
            window?.Dispose();
        }

        private static void OnUpdate(double deltaTime)
        {
        }
        private static void OnResize(Silk.NET.Maths.Vector2D<int> newSize)
        {
            if (opengl != null)
                opengl.Viewport(0, 0, (uint)newSize.X, (uint)newSize.Y);
        }
        private static void OnRender(double deltaTime)
        {
            currDeltaTime = deltaTime;

            opengl.ClearColor(0.12f, 0.12f, 0.12f, 1.0f);
            opengl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            currScene = activeScenes.Count > 0 ? activeScenes[currSceneIndex] : null;

            TakeCamInputs();

            if (currScene != null && currScene.mainCamera != null)
            {
                Camera cam = currScene.mainCamera;

                //if any cam vectoir changed
                bool camMoved = cam.position.x != lastCamPos.x || 
                                cam.position.y != lastCamPos.y || 
                                cam.position.z != lastCamPos.z ||
                                cam.forward.x != lastCamForward.x || 
                                cam.forward.y != lastCamForward.y || 
                                cam.forward.z != lastCamForward.z;

                if (camMoved || currScene.isOutdated || currRenderMode != lastRenderMode)
                {
                    lastCamPos = cam.position;
                    lastCamForward = cam.forward;
                    lastRenderMode = currRenderMode;
                    needsRender = true;
                }

                if (needsRender && !isRendering)
                {
                    isRendering = true;

                    Scene capturedScene = currScene;
                    int capturedMode = currRenderMode;

                    renderTask = System.Threading.Tasks.Task.Run(() =>
                        Renderer.RenderGPU(capturedScene, capturedMode)
                    );
                }

                //if task done push to opengl gpu
                else if (renderTask != null && renderTask.IsCompleted)
                {
                    Image renderResult = renderTask.Result;
                    RenderToTexture(renderResult.data, (uint)renderResult.width, (uint)renderResult.height);

                    isRendering = false;
                    needsRender = false;
                    currScene.isOutdated = false;
                }
            }

            guiController.Update((float)deltaTime);
            BuildUI();
            guiController.Render();
        }

        private static void UpdateCamPos(Camera cam)
        {
            ///now instead use spherical coords rather than a pivot, pitch and orbit
            ///much smoother prevents gimbal lock and natural
            float x = pivot.x + camRadius * MathF.Cos(camPitch) * MathF.Sin(camYaw);
            float y = pivot.y + camRadius * MathF.Sin(camPitch);
            float z = pivot.z + camRadius * MathF.Cos(camPitch) * MathF.Cos(camYaw);

            cam.position = new Math.Vector3(x, y, z);

            //make sure cam and vp aspect ratio are matched
            if (currentViewportSize.Y > 0)
                cam.vpWidth = cam.vpHeight * (currentViewportSize.X / currentViewportSize.Y);

            cam.RotateTo(pivot);
            needsRender = true;
        }
        private static void TakeCamInputs()
        {
            if (mouse == null || currScene == null || currScene.mainCamera == null) return;

            Vector2 mousePos = new Vector2(mouse.Position.X, mouse.Position.Y);
            Vector2 delta = mousePos - lastMousePos;
            lastMousePos = mousePos;

            if (!isViewportHovered)
            {
                scrollDelta = 0.0f;
                return;
            }

            //orbit middle click
            if (mouse.IsButtonPressed(MouseButton.Middle))
            {
                if (delta.X != 0 || delta.Y != 0)
                {
                    float sensitivity = 0.005f;
                    camYaw += delta.X * sensitivity;
                    camPitch += delta.Y * sensitivity;

                    //1.57 rad clamps rotation to 89deg to prevent gimbal lock
                    float maxPitch = 1.55f;
                    if (camPitch > maxPitch) camPitch = maxPitch;
                    if (camPitch < -maxPitch) camPitch = -maxPitch;
                }
            }
            //pan right click
            else if (mouse.IsButtonPressed(MouseButton.Right))
            {
                if (delta.X != 0 || delta.Y != 0)
                {
                    float panSpeed = 0.001f * camRadius; //scale pan velocity with distance 
                    Math.Vector3 panOffset = (currScene.mainCamera.right * (-delta.X * panSpeed)) - (currScene.mainCamera.up * -delta.Y * panSpeed);
                    pivot += panOffset;
                }
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                leftClickStartPos = mousePos;

            //drag left click and hold
            else if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && selectedTool != Tool.None)
            {

                //transform geo
                if (currSelection == (int)SelectionType.Geometry && currObjectIndex >= 0 && currObjectIndex < currScene.objects.Count)
                {
                    Geometry.Object selObj = currScene.objects[currObjectIndex];

                    if (selectedTool == Tool.Move)
                    {
                        float snapThreshold = 1.5f;

                        float moveSpeed = 0.002f * camRadius;
                        Math.Vector3 moveOffset = (currScene.mainCamera.right * delta.X * moveSpeed) - (currScene.mainCamera.up * delta.Y * moveSpeed);
                        //moveoffset is projected worldspace movement which matches screenspace camera movement

                        float absX = System.Math.Abs(moveOffset.x), absY = System.Math.Abs(moveOffset.y), absZ = System.Math.Abs(moveOffset.z);

                        //snapthreshold works by checking if one axis was moved *snapThreshold more than others, then lockin pos to it.
                        if (absX > absY * snapThreshold && absX > absZ * snapThreshold){
                            moveOffset = new Math.Vector3(moveOffset.x, 0, 0);
                            activeAxis = "X";
                        }
                        else if (absY > absX * snapThreshold && absY > absZ * snapThreshold) {
                            moveOffset = new Math.Vector3(0, moveOffset.y, 0);
                            activeAxis = "Y";
                        }
                        else if (absZ > absX * snapThreshold && absZ > absY * snapThreshold) {
                            moveOffset = new Math.Vector3(0, 0, moveOffset.z);
                            activeAxis = "Z";
                        }
                        else activeAxis = "";

                        selObj.position += moveOffset;
                        selObj.needsPrecomp = true;
                        needsRender = true;
                    }
                    else if (selectedTool == Tool.Rotate)
                    {
                        float snapThreshold = 0.5f;

                        float rotSpeed = 0.5f;
                        float rotX = delta.Y * rotSpeed;
                        float rotY = -delta.X * rotSpeed;

                        if (System.Math.Abs(rotX) > System.Math.Abs(rotY) * snapThreshold) 
                            rotY = 0;
                        else if (System.Math.Abs(rotY) > System.Math.Abs(rotX) * snapThreshold) 
                            rotX = 0;

                        selObj.rotation.x += rotX;
                        selObj.rotation.y += rotY;
                        selObj.needsPrecomp = true;
                    }
                    else if (selectedTool == Tool.Scale)
                    {
                        selObj.scale += (delta.X - delta.Y) * 0.01f;
                        if (selObj.scale < 0.01f) selObj.scale = 0.01f;
                        selObj.needsPrecomp = true;
                    }
                }

                //transform lamps
                else if (currSelection == (int)SelectionType.Lamp && currLampIndex >= 0 && currLampIndex < currScene.lamps.Count)
                {
                    if (selectedTool == Tool.Move)
                    {
                        float snapThreshold = 1.5f;

                        float moveSpeed = 0.002f * camRadius;
                        Math.Vector3 moveOffset = (currScene.mainCamera.right * delta.X * moveSpeed) - (currScene.mainCamera.up * delta.Y * moveSpeed);

                        float absX = System.Math.Abs(moveOffset.x), absY = System.Math.Abs(moveOffset.y), absZ = System.Math.Abs(moveOffset.z);
                        if (absX > absY * snapThreshold && absX > absZ * snapThreshold)
                        {
                            moveOffset = new Math.Vector3(moveOffset.x, 0, 0);
                            activeAxis = "X";
                        }
                        else if (absY > absX * snapThreshold && absY > absZ * snapThreshold)
                        {
                            moveOffset = new Math.Vector3(0, moveOffset.y, 0);
                            activeAxis = "Y";
                        }
                        else if (absZ > absX * snapThreshold && absZ > absY * snapThreshold)
                        {
                            moveOffset = new Math.Vector3(0, 0, moveOffset.z);
                            activeAxis = "Z";
                        }
                        else activeAxis = "";

                        currScene.lamps[currLampIndex].position += moveOffset;
                        currScene.isOutdated = true; //need to trigger full rebuild as lamps dont precomp data -no geo
                    }
                }
            }
            //select left click
            else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                Vector2 localMouse = mousePos - currentViewportMin;
                float targetX = (localMouse.X / currentViewportSize.X) * Config.imgWidth;
                float targetY = ((currentViewportSize.Y - localMouse.Y) / currentViewportSize.Y) * Config.imgHeight;

                Camera cam = currScene.mainCamera;
                Raycast ray = cam.ShootRay(targetX, targetY);

                float closestDist = float.MaxValue;
                SelectionType hitType = SelectionType.None;
                int hitIndex = -1;

                //raycast for geometry hits
                for (int i = 0; i < currScene.objects.Count; i++)
                {
                    if (!currScene.objects[i].isVisible) continue;
                    foreach (Triangle tri in currScene.objects[i].bakedTriangles)
                    {
                        HitInfo? hit = Utils.GetRayIntersect(ray, tri);
                        if (hit.HasValue && hit.Value.distance < closestDist)
                        {
                            closestDist = hit.Value.distance;
                            hitType = SelectionType.Geometry;
                            hitIndex = i;
                        }
                    }
                }
                //raycast for lamps, emulate a sphere raycast
                for (int i = 0; i < currScene.lamps.Count; i++)
                {
                    if (!currScene.lamps[i].isVisible) continue;

                    Math.Vector3 vecToLamp = cam.position - currScene.lamps[i].position;
                    float pointAlongTheVecToLampWhereTheDistanceFromTheVectorPathToSaidLampIsLowest = Math.Utils.Dot(vecToLamp, ray.direction);
                    float distanceToLamp = Math.Utils.Dot(vecToLamp, vecToLamp) - 0.25f; //radius of lamp
                    float discriminant = pointAlongTheVecToLampWhereTheDistanceFromTheVectorPathToSaidLampIsLowest * pointAlongTheVecToLampWhereTheDistanceFromTheVectorPathToSaidLampIsLowest - distanceToLamp;

                    if (discriminant > 0.0f)
                    {
                        float t = -pointAlongTheVecToLampWhereTheDistanceFromTheVectorPathToSaidLampIsLowest - MathF.Sqrt(discriminant);
                        if (t > 0.001f && t < closestDist)
                        {
                            closestDist = t;
                            hitType = SelectionType.Lamp;
                            hitIndex = i;
                        }
                    }
                }

                //set vars to that obj and type
                currSelection = (int)hitType;

                if (hitType == SelectionType.Geometry) 
                { currObjectIndex = hitIndex; currLampIndex = -1; }
                else if (hitType == SelectionType.Lamp) 
                { currLampIndex = hitIndex; currObjectIndex = -1; }
                else { currObjectIndex = -1; currLampIndex = -1; }
            }

            //zoom scroll
            if (scrollDelta != 0.0f)
            {
                float zoomSpeed = 0.15f * camRadius; //scale zoom velocity with distance 
                camRadius -= scrollDelta * zoomSpeed;
                if (camRadius < 0.5f) camRadius = 0.5f; //prevent zooming thru pivot
                scrollDelta = 0.0f;
            }

            UpdateCamPos(currScene.mainCamera);
        }
        private static void RenderToTexture(byte[] pixelData, uint width, uint height)
        {
            if (currTextureId == 0)
                currTextureId = opengl.GenTexture();

            opengl.BindTexture(TextureTarget.Texture2D, currTextureId);
            opengl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            opengl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            unsafe {
                fixed (byte* index = pixelData)
                opengl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb, width, height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, index);
            }

            opengl.BindTexture(TextureTarget.Texture2D, 0);
        }
        private static void DrawJarvisSystem(ImDrawListPtr drawList, Vector2 vpMin, Vector2 vpSize)
        {
            if (currScene == null || currScene.mainCamera == null) return;
            Camera cam = currScene.mainCamera;

            //helper to project a world coordinate to a screen coordinate
            bool WorldToScreen(Math.Vector3 worldPos, out Vector2 screenPos, out float z)
            {
                screenPos = new Vector2(0, 0);
                Math.Vector3 vector = new Math.Vector3(
                    worldPos.x - cam.position.x,
                    worldPos.y - cam.position.y,
                    worldPos.z - cam.position.z);

                z = Math.Utils.Dot(vector, cam.forward);
                //skip infinite
                if (z <= 0.001f) return false;

                float screenX = Math.Utils.Dot(vector, cam.right) / z;
                float screenY = Math.Utils.Dot(vector, cam.up) / z;

                screenPos.X = vpMin.X + vpSize.X * (0.5f + screenX / cam.vpWidth);
                screenPos.Y = vpMin.Y + vpSize.Y * (0.5f - screenY / cam.vpHeight);
                return true;
            }

            //draw a line in 3d space to screen
            void CreateLine(Math.Vector3 point1, Math.Vector3 point2, uint color, float thickness)
            {
                Math.Vector3 v1 = new Math.Vector3(point1.x - cam.position.x, point1.y - cam.position.y, point1.z - cam.position.z);
                Math.Vector3 v2 = new Math.Vector3(point2.x - cam.position.x, point2.y - cam.position.y, point2.z - cam.position.z);

                float depth1 = Math.Utils.Dot(v1, cam.forward);
                float depth2 = Math.Utils.Dot(v2, cam.forward);
                float closePlane = 0.1f;

                if (depth1 < closePlane && depth2 < closePlane) 
                    return; //the line is behind cam so skip

                //clips the line to the close planme
                if (depth1 < closePlane)
                {
                    float time = (closePlane - depth1) / (depth2 - depth1);
                    point1 = new Math.Vector3(
                        point1.x + (point2.x - point1.x) 
                        * time, point1.y + (point2.y - point1.y) 
                        * time, point1.z + (point2.z - point1.z) 
                        * time);
                }
                else if (depth2 < closePlane)
                {
                    float time = (closePlane - depth2) / (depth1 - depth2);
                    point2 = new Math.Vector3(
                        point2.x + (point1.x - point2.x) 
                        * time, point2.y + (point1.y - point2.y) 
                        * time, point2.z + (point1.z - point2.z) 
                        * time);
                }

                if (WorldToScreen(point1, out Vector2 s1, out _) && WorldToScreen(point2, out Vector2 s2, out _))
                    drawList.AddLine(s1, s2, color, thickness);
            }

            uint gridColor = ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 0.2f));
            uint colorX = ImGui.GetColorU32(new Vector4(1.0f, 0.2f, 0.3f, 0.8f)); // Red
            uint colorY = ImGui.GetColorU32(new Vector4(0.4f, 0.9f, 0.2f, 0.8f)); // Green
            uint colorZ = ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 0.8f)); // Blue

            //draw flat grid
            int gridSize = 10;
            for (int i = -gridSize; i <= gridSize; i++)
            {
                if (i == 0) continue; //skip axis
                CreateLine(new Math.Vector3(i, 0, -gridSize), new Math.Vector3(i, 0, gridSize), gridColor, 1.0f);
                CreateLine(new Math.Vector3(-gridSize, 0, i), new Math.Vector3(gridSize, 0, i), gridColor, 1.0f);
            }

            //draw colour axis
            CreateLine(new Math.Vector3(-gridSize, 0, 0), new Math.Vector3(gridSize, 0, 0), colorX, 2.0f); //x
            CreateLine(new Math.Vector3(0, 0, 0), new Math.Vector3(0, gridSize, 0), colorY, 2.0f); //y
            CreateLine(new Math.Vector3(0, 0, -gridSize), new Math.Vector3(0, 0, gridSize), colorZ, 2.0f); //z

            //lamp markers
            uint lampColor = ImGui.GetColorU32(new Vector4(1.0f, 0.9f, 0.4f, 1.0f));
            for (int i = 0; i < currScene.lamps.Count; i++)
            {
                if (!currScene.lamps[i].isVisible) continue;
                Math.Vector3 pos = currScene.lamps[i].position;
                float size = 0.3f;

                //3d crosshair
                CreateLine(new Math.Vector3(pos.x - size, pos.y, pos.z), new Math.Vector3(pos.x + size, pos.y, pos.z), lampColor, 2.0f);
                CreateLine(new Math.Vector3(pos.x, pos.y - size, pos.z), new Math.Vector3(pos.x, pos.y + size, pos.z), lampColor, 2.0f);
                CreateLine(new Math.Vector3(pos.x, pos.y, pos.z - size), new Math.Vector3(pos.x, pos.y, pos.z + size), lampColor, 2.0f);

                if (currSelection == (int)SelectionType.Lamp && currLampIndex == i)
                {
                    CreateLine(new Math.Vector3(pos.x - size, pos.y - size, pos.z), new Math.Vector3(pos.x + size, pos.y + size, pos.z), ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 1.5f);
                    CreateLine(new Math.Vector3(pos.x - size, pos.y + size, pos.z), new Math.Vector3(pos.x + size, pos.y - size, pos.z), ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 1.5f);
                }
            }

            //selected obj outline AND GIZMOS draw
            if (currObjectIndex >= 0 && currObjectIndex < currScene.objects.Count)
            {
                Geometry.Object selObj = currScene.objects[currObjectIndex];

                //blue wireframe for selected obj
                uint outlineColor = ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 1.0f));
                foreach (Triangle tri in selObj.bakedTriangles) //FIX THIS 
                {
                    CreateLine(tri.a, tri.b, outlineColor, 0.5f);
                    CreateLine(tri.b, tri.c, outlineColor, 0.5f);
                    CreateLine(tri.c, tri.a, outlineColor, 0.5f);
                }
            }

            //tool gizmos for move, rotate and scale
            if (selectedTool != Tool.None)
            {
                Math.Vector3? gizmoPos = null;

                if (currSelection == (int)SelectionType.Geometry && currObjectIndex >= 0 && currObjectIndex < currScene.objects.Count)
                    gizmoPos = currScene.objects[currObjectIndex].position;
                else if (currSelection == (int)SelectionType.Lamp && currLampIndex >= 0 && currLampIndex < currScene.lamps.Count)
                    gizmoPos = currScene.lamps[currLampIndex].position;

                if (gizmoPos.HasValue)
                {
                    float size = 1.5f;
                    Math.Vector3 pos = gizmoPos.Value;

                    CreateLine(pos, new Math.Vector3(pos.x + size, pos.y, pos.z), ImGui.GetColorU32(new Vector4(1, 0, 0, 1)), 4.0f); //x red
                    CreateLine(pos, new Math.Vector3(pos.x, pos.y + size, pos.z), ImGui.GetColorU32(new Vector4(0, 1, 0, 1)), 4.0f); //y green
                    CreateLine(pos, new Math.Vector3(pos.x, pos.y, pos.z + size), ImGui.GetColorU32(new Vector4(0, 0, 1, 1)), 4.0f); //z blue
                }

                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && activeAxis != "")
                {
                    Math.Vector3 p = gizmoPos.Value;
                    if (activeAxis == "X")
                        CreateLine(new Math.Vector3(-9999f, p.y, p.z), new Math.Vector3(9999f, p.y, p.z), colorX, 1.5f);
                    if (activeAxis == "Y")
                        CreateLine(new Math.Vector3(p.x, -9999f, p.z), new Math.Vector3(p.x, 9999f, p.z), colorY, 1.5f);
                    if (activeAxis == "Z")
                        CreateLine(new Math.Vector3(p.x, p.y, -9999f), new Math.Vector3(p.x, p.y, 9999f), colorZ, 1.5f);
                }
                else
                {
                    activeAxis = "";
                }
            }

            //draw bvh overlay
            if (showBvh && currScene.rootBVH != null)
            {
                if (showBvh && currScene.rootBVH != null)
                {
                    //find bvh max depth for colour interp
                    //should really do this in the bvh node itself but this is less complex
                    int GetMaxDepth(BoundingVolume node)
                    {
                        if (node == null) return 0;
                        return 1 + System.Math.Max(GetMaxDepth(node.left), GetMaxDepth(node.right));
                    }
                    int maxDepth = GetMaxDepth(currScene.rootBVH);

                    void DrawBVHNode(BoundingVolume node, int depth)
                    {
                        if (node == null) return;

                        //interp factor 0 to 1 heading deeper
                        float t = maxDepth > 1 ? (float)depth / (maxDepth - 1) : 0f;

                        //red to blue
                        float r = 1.0f - t;
                        float g = 0.2f;
                        float b = t;
                        uint bvhColor = ImGui.GetColorU32(new Vector4(r, g, b, 0.4f));

                        Math.Vector3 min = node.boundsMin;
                        Math.Vector3 max = node.boundsMax;

                        Math.Vector3 c0 = new Math.Vector3(min.x, min.y, min.z);
                        Math.Vector3 c1 = new Math.Vector3(max.x, min.y, min.z);
                        Math.Vector3 c2 = new Math.Vector3(max.x, max.y, min.z);
                        Math.Vector3 c3 = new Math.Vector3(min.x, max.y, min.z);
                        Math.Vector3 c4 = new Math.Vector3(min.x, min.y, max.z);
                        Math.Vector3 c5 = new Math.Vector3(max.x, min.y, max.z);
                        Math.Vector3 c6 = new Math.Vector3(max.x, max.y, max.z);
                        Math.Vector3 c7 = new Math.Vector3(min.x, max.y, max.z);

                        CreateLine(c0, c1, bvhColor, 1.0f); CreateLine(c1, c2, bvhColor, 1.0f);
                        CreateLine(c2, c3, bvhColor, 1.0f); CreateLine(c3, c0, bvhColor, 1.0f);
                        CreateLine(c4, c5, bvhColor, 1.0f); CreateLine(c5, c6, bvhColor, 1.0f);
                        CreateLine(c6, c7, bvhColor, 1.0f); CreateLine(c7, c4, bvhColor, 1.0f);
                        CreateLine(c0, c4, bvhColor, 1.0f); CreateLine(c1, c5, bvhColor, 1.0f);
                        CreateLine(c2, c6, bvhColor, 1.0f); CreateLine(c3, c7, bvhColor, 1.0f);

                        DrawBVHNode(node.left, depth + 1);
                        DrawBVHNode(node.right, depth + 1);
                    }

                    DrawBVHNode(currScene.rootBVH, 0);
                }
            }
        }

        private static void BuildUI()
        {
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            ImGui.SetNextWindowPos(viewport.WorkPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(viewport.WorkSize, ImGuiCond.Always);

            ImGuiWindowFlags hostFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                                         ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                                         ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus
                                         | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar;

            #region Main Window
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
            ImGui.Begin("MainWorkspaceHost", hostFlags);
            ImGui.PopStyleVar(2);

            Vector2 vpSize = viewport.WorkSize;
            float leftWidth = vpSize.X * 0.22f;
            float rightWidth = vpSize.X * 0.25f;
            float viewportWidth = vpSize.X - leftWidth - rightWidth;
            float bottomHeight = vpSize.Y * 0.12f;
            float mainHeight = vpSize.Y - bottomHeight;
            #endregion

            #region Left Column
            ImGui.BeginChild("LeftColumnChild", new Vector2(leftWidth, mainHeight), ImGuiChildFlags.None);

            #region Scene Manager
            ImGui.BeginChild("SceneInfoChild", new Vector2(leftWidth, mainHeight * 0.55f), ImGuiChildFlags.Border);
            ImGui.Text("Scene Manager");
            ImGui.Separator();
            ImGui.Spacing();

            string[] sceneNames = new string[activeScenes.Count];
            for (int i = 0; i < activeScenes.Count; i++)
                sceneNames[i] = $"Scene {i + 1}";

            ImGui.Combo("Active Scene", ref currSceneIndex, sceneNames, sceneNames.Length);
            if (ImGui.Button("Create New Scene", new Vector2(-1, 30)))
                CreateNewScene();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            currScene = activeScenes.Count > 0 ? activeScenes[currSceneIndex] : null;
            if (currScene != null)
            {
                int totalVerts = 0, totalTris = 0;
                foreach (var obj in currScene.objects)
                {
                    totalVerts += obj.mesh.vertices.Count;
                    totalTris += obj.bakedTriangles.Count;
                }

                ImGui.Text($"Total Objects: {currScene.objects.Count}");
                ImGui.Text($"Total Lamps: {currScene.lamps.Count}");
                ImGui.TextDisabled($"Vertices: {totalVerts} | Triangles: {totalTris}");
                ImGui.Spacing();

                if (currObjectIndex >= 0 && currObjectIndex < currScene.objects.Count)
                {
                    Geometry.Object selObj = currScene.objects[currObjectIndex];
                    ImGui.Text($"Selected Object: {selObj.name}");
                    ImGui.Text($"Object Pos: ({selObj.position.x:F2}, {selObj.position.y:F2}, {selObj.position.z:F2})");
                }
                else
                {
                    ImGui.Text("Selected Object: None");
                    ImGui.Text("Object Pos: (0.00, 0.00, 0.00)");
                }

                ImGui.Spacing();
                ImGui.Text($"Camera Pos: ({currScene.mainCamera.position.x:F2}, {currScene.mainCamera.position.y:F2}, {currScene.mainCamera.position.z:F2})");
            }

            ImGui.EndChild();
            #endregion

            #region Outliner & Importer
            ImGui.BeginChild("ImportManagerChild", new Vector2(leftWidth, mainHeight * 0.45f), ImGuiChildFlags.Border);
            ImGui.Text("Outliner");
            ImGui.Separator();

            if (ImGui.Button("Import .OBJ File", new Vector2(-1, 30)))
                ImGui.OpenPopup("ImportModelPopup");
            if (ImGui.BeginPopup("ImportModelPopup"))
            {
                ImGui.TextDisabled("Select a model");
                ImGui.Separator();

                string geoPath = Path.Combine(projectDirectory, "Geometry");
                if (Directory.Exists(geoPath))
                {
                    string[] objFiles = Directory.GetFiles(geoPath, "*.obj");

                    if (objFiles.Length == 0)
                        ImGui.TextDisabled("No models found.");
                    else
                        foreach (string file in objFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            if (ImGui.Selectable(fileName))
                                if (currScene != null)
                                {
                                    string objName = Path.GetFileNameWithoutExtension(file);
                                    Geometry.Object newObj = MeshLib.NewObj(file, new Math.Vector3(0, 0, 0), objName, 1.0f);
                                    newObj.material = new Material($"{objName} Material", new Math.Vector3(0.8f, 0.8f, 0.8f), 0.5f, 0.5f);

                                    currScene.AddObject(newObj);
                                    currObjectIndex = currScene.objects.Count - 1;
                                }
                        }
                }
                else ImGui.TextDisabled("Geometry folder not found.");

                ImGui.EndPopup();
            }
            ImGui.Spacing();

            if (ImGui.Button("Add Point Lamp", new Vector2(-1, 30)))
            {
                if (currScene != null)
                {
                    // Spawn the lamp slightly above the origin
                    currScene.AddLamp(new Lamp(new Math.Vector3(0, 2.0f, 0), 50.0f));

                    // Automatically select it!
                    currSelection = (int)SelectionType.Lamp;
                    currLampIndex = currScene.lamps.Count - 1;
                    currObjectIndex = -1;

                    // Alert the GPU
                    currScene.isOutdated = true;
                }
            }
            ImGui.Spacing();

            if (currScene != null)
            {
                ImGui.TextDisabled("--- Geometry ---");
                for (int i = 0; i < currScene.objects.Count; i++)
                {
                    bool vis = currScene.objects[i].isVisible;
                    if (ImGui.Checkbox($"##objVis{i}", ref vis))
                    {
                        currScene.objects[i].isVisible = vis;
                        currScene.isOutdated = true; //rebuild 
                    }
                    ImGui.SameLine();

                    if (ImGui.Selectable(currScene.objects[i].name, currSelection == (int)SelectionType.Geometry && currObjectIndex == i))
                    {
                        currSelection = (int)SelectionType.Geometry;
                        currObjectIndex = i;
                    }
                }

                ImGui.Spacing();
                ImGui.TextDisabled("--- Lamps ---");
                for (int i = 0; i < currScene.lamps.Count; i++)
                {
                    bool vis = currScene.lamps[i].isVisible;
                    if (ImGui.Checkbox($"##lampVis{i}", ref vis))
                    {
                        currScene.lamps[i].isVisible = vis;
                        currScene.isOutdated = true; //rebuild
                    }
                    ImGui.SameLine();

                    if (ImGui.Selectable($"{currScene.lamps[i].name}##lamp{i}", currSelection == (int)SelectionType.Lamp && currLampIndex == i))
                    {
                        currSelection = (int)SelectionType.Lamp;
                        currLampIndex = i;
                        currObjectIndex = -1;
                    }
                }
            }

            ImGui.EndChild();
            #endregion

            ImGui.EndChild();
            #endregion

            ImGui.SameLine();

            #region Viewport Area (Center)
            ImGui.BeginChild("ViewportChild", new Vector2(viewportWidth, mainHeight), ImGuiChildFlags.Border);
            Vector2 defViewportSize = ImGui.GetContentRegionAvail();
            currentViewportSize = defViewportSize;
            currentViewportMin = ImGui.GetCursorScreenPos();

            if (currTextureId != 0)
            {
                // flip the image to match backend renderer
                ImGui.Image((IntPtr)currTextureId, currentViewportSize, new Vector2(0, 1), new Vector2(1, 0));

                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                DrawJarvisSystem(drawList, currentViewportMin, currentViewportSize);
            }
            else
            {
                ImGui.SetCursorPos(new Vector2((currentViewportSize.X * 0.5f) - 100, currentViewportSize.Y * 0.5f));
                ImGui.Text("Loading Viewport...");
            }
            isViewportHovered = ImGui.IsItemHovered();

            ImGui.EndChild();
            #endregion

            ImGui.SameLine();

            #region Right Column
            ImGui.BeginChild("RightColumnChild", new Vector2(rightWidth, mainHeight), ImGuiChildFlags.None);

            #region Material & Properties Settings
            ImGui.BeginChild("MaterialChild", new Vector2(rightWidth, mainHeight * 0.33f), ImGuiChildFlags.Border);

            if (currScene != null && currSelection == (int)SelectionType.Geometry && currObjectIndex >= 0)
            {
                ImGui.Text("Material Properties");
                ImGui.Separator();
                Geometry.Object selectedObj = currScene.objects[currObjectIndex];
                Material mat = selectedObj.material;

                if (ImGui.Button("Load Texture", new Vector2(-1, 24)))
                    ImGui.OpenPopup("LoadPbrPopup");

                if (ImGui.BeginPopup("LoadPbrPopup"))
                {
                    string texPath = Path.Combine(projectDirectory, "Textures");
                    if (Directory.Exists(texPath))
                    {
                        string[] albedoFiles = Directory.GetFiles(texPath, "*_Albedo.*");

                        foreach (string file in albedoFiles)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            string prefix = fileName.Replace("_Albedo", "");

                            if (ImGui.Selectable(prefix))
                            {
                                selectedObj.material.LoadPBRTexture(texPath, prefix);
                                selectedObj.needsPrecomp = true;
                                needsRender = true;
                            }
                        }
                    }
                    ImGui.EndPopup();
                }

                System.Numerics.Vector3 albedoColor = new System.Numerics.Vector3(mat.colour.x, mat.colour.y, mat.colour.z);
                if (ImGui.ColorEdit3("Diffuse", ref albedoColor))
                {
                    mat.colour = new Math.Vector3(albedoColor.X, albedoColor.Y, albedoColor.Z);
                    selectedObj.needsPrecomp = true;
                    needsRender = true;
                }
                float spec = mat.specular;
                if (ImGui.DragFloat("Specular", ref spec, 0.005f, 0.0f, 1.0f))
                {
                    mat.specular = spec;
                    selectedObj.needsPrecomp = true;
                    needsRender = true;
                }
                float rough = mat.roughness;
                if (ImGui.DragFloat("Roughness", ref rough, 0.005f, 0.0f, 1.0f))
                {
                    mat.roughness = rough;
                    selectedObj.needsPrecomp = true;
                    needsRender = true;
                }
                float met = mat.metallic;
                if (ImGui.DragFloat("Metallic", ref met, 0.005f, 0.0f, 1.0f))
                {
                    mat.metallic = met;
                    selectedObj.needsPrecomp = true;
                    needsRender = true;
                }

                if (ImGui.Button("Delete Object", new Vector2(-1, 24)))
                {
                    currScene.objects.RemoveAt(currObjectIndex);
                    currSelection = (int)SelectionType.None;
                    currScene.BuildBVH();
                    currScene.isOutdated = true;
                }
            }
            else if (currScene != null && currSelection == (int)SelectionType.Lamp && currLampIndex >= 0)
            {
                ImGui.Text("Lamp Properties");
                ImGui.Separator();
                Lamp selLamp = currScene.lamps[currLampIndex];

                float bright = selLamp.brightness;
                if (ImGui.DragFloat("Brightness", ref bright, 0.5f, 0.0f, 1000.0f)) 
                {
                    selLamp.brightness = bright;
                    currScene.isOutdated = true;
                }

                if (ImGui.Button("Delete Lamp", new Vector2(-1, 24)))
                {
                    currScene.lamps.RemoveAt(currLampIndex);
                    currSelection = (int)SelectionType.None;
                    currScene.isOutdated = true;
                }
            }
            else
            {
                ImGui.Text("Properties");
                ImGui.Separator();
                ImGui.TextDisabled("No item selected.");
            }

            ImGui.EndChild();
            #endregion

            #region World Settings
            ImGui.BeginChild("WorldChild", new Vector2(rightWidth, mainHeight * 0.33f), ImGuiChildFlags.Border);
            ImGui.Text("Environment Settings");
            ImGui.Separator();

            System.Numerics.Vector3 bgColor = new System.Numerics.Vector3(Config.bgColour[0] / 255f, Config.bgColour[1] / 255f, Config.bgColour[2] / 255f);
            if (ImGui.ColorEdit3("Background Colour", ref bgColor))
            {
                Config.bgColour[0] = (int)(bgColor.X * 255);
                Config.bgColour[1] = (int)(bgColor.Y * 255);
                Config.bgColour[2] = (int)(bgColor.Z * 255);
                needsRender = true;
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (currScene != null)
            {
                bool isHdriEnabled = currScene.useHdri;
                if (ImGui.Checkbox("Enable HDRI", ref isHdriEnabled))
                {
                    currScene.useHdri = isHdriEnabled;

                    if (isHdriEnabled && currScene.hdriWidth == 1 && hdriFiles.Length > 0)
                    {
                        currHdriIndex = 0;
                        currScene.SetHDRI(hdriFiles[0]);
                    }
                }

                if (ImGui.SliderFloat("HDRI Intensity", ref currScene.hdriIntensity, 0.0f, 10.0f))
                    currScene.isOutdated = true;

                if (hdriFiles.Length > 0)
                {
                    string[] hdriNames = new string[hdriFiles.Length];
                    for (int i = 0; i < hdriFiles.Length; i++)
                        hdriNames[i] = Path.GetFileName(hdriFiles[i]);

                    if (ImGui.Combo("Select HDRI", ref currHdriIndex, hdriNames, hdriNames.Length))
                    {
                        currScene.SetHDRI(hdriFiles[currHdriIndex]);
                        currScene.useHdri = true;
                        needsRender = true;
                    }
                }
                else
                {
                    ImGui.TextDisabled("No .hdr files in /HDRIs/");
                }
            }
            ImGui.EndChild();
            #endregion

            #region Render Settings
            ImGui.BeginChild("RenderChild", new Vector2(rightWidth, mainHeight * 0.34f), ImGuiChildFlags.Border);
            ImGui.Text("Renderer Configuration");

            double fps = currDeltaTime > 0 ? 1.0 / currDeltaTime : 0;
            ImGui.TextDisabled($"Performance: {(currDeltaTime * 1000).ToString("0.00")} ms ({fps.ToString("0")} FPS)");

            ImGui.Separator();
            int samples = Config.maxSamples;
            int depth = Config.maxRayDepth;

            //dragint better scaling
            if (ImGui.DragInt("Max Samples", ref samples, 1f, 1, 256)) { 
                Config.maxSamples = samples;
                needsRender = true;
            }
            if (ImGui.DragInt("Ray Depth", ref depth, 0.1f, 1, 16)) { 
                Config.maxRayDepth = depth;
                needsRender = true;
            }

            ImGui.EndChild();
            #endregion

            ImGui.EndChild();
            #endregion

            #region Bottom Bar
            ImGui.BeginChild("BottomStripChild", new Vector2(vpSize.X, bottomHeight), ImGuiChildFlags.Border);

            //draw the highlighted button if its selected
            void RenderModeBtn(string label, int mode)
            {
                if (currRenderMode == mode) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                if (ImGui.Button(label)) currRenderMode = mode;
                if (currRenderMode == mode) ImGui.PopStyleColor();
                ImGui.SameLine();
            }

            RenderModeBtn("Depth", 4);
            RenderModeBtn("Normal", 1);
            RenderModeBtn("Diffuse", 3);
            RenderModeBtn("Traced", 2);

            ImGui.SameLine();

            ImGui.Checkbox("BVH Overlay", ref showBvh);

            ImGui.SameLine();
            ImGui.Text(" | Tools: ");
            ImGui.SameLine();
            if (ImGui.Button(selectedTool == Tool.Move ? "[ Move ]" : "Move"))
                selectedTool = selectedTool == Tool.Move ? Tool.None : Tool.Move;
            ImGui.SameLine();
            if (ImGui.Button(selectedTool == Tool.Rotate ? "[ Rotate ]" : "Rotate"))
                selectedTool = selectedTool == Tool.Rotate ? Tool.None : Tool.Rotate;
            ImGui.SameLine();
            if (ImGui.Button(selectedTool == Tool.Scale ? "[ Scale ]" : "Scale"))
                selectedTool = selectedTool == Tool.Scale ? Tool.None : Tool.Scale;
            ImGui.EndChild();
            #endregion

            ImGui.End();
        }
        private static void SwitchToLight()
        {
            var style = ImGui.GetStyle();

            style.WindowPadding = new Vector2(15.0f, 15.0f);
            style.WindowRounding = 5.0f;
            style.FramePadding = new Vector2(5.0f, 5.0f);
            style.FrameRounding = 4.0f;
            style.ItemSpacing = new Vector2(12.0f, 8.0f);
            style.ItemInnerSpacing = new Vector2(8.0f, 6.0f);
            style.IndentSpacing = 25.0f;
            style.ScrollbarSize = 15.0f;
            style.ScrollbarRounding = 9.0f;
            style.GrabMinSize = 5.0f;
            style.GrabRounding = 3.0f;

            unsafe
            {
                var colors = style.Colors;

                colors[(int)ImGuiCol.Text] = new Vector4(0.40f, 0.39f, 0.38f, 1.00f);
                colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.40f, 0.39f, 0.38f, 0.77f);
                colors[(int)ImGuiCol.WindowBg] = new Vector4(0.92f, 0.91f, 0.88f, 0.70f);
                colors[(int)ImGuiCol.ChildBg] = new Vector4(1.00f, 0.98f, 0.95f, 0.58f);
                colors[(int)ImGuiCol.PopupBg] = new Vector4(0.92f, 0.91f, 0.88f, 0.92f);
                colors[(int)ImGuiCol.Border] = new Vector4(0.84f, 0.83f, 0.80f, 0.65f);
                colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.92f, 0.91f, 0.88f, 0.00f);
                colors[(int)ImGuiCol.FrameBg] = new Vector4(1.00f, 0.98f, 0.95f, 1.00f);
                colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.99f, 1.00f, 0.40f, 0.78f);
                colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.26f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.TitleBg] = new Vector4(1.00f, 0.98f, 0.95f, 1.00f);
                colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(1.00f, 0.98f, 0.95f, 0.75f);
                colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.MenuBarBg] = new Vector4(1.00f, 0.98f, 0.95f, 0.47f);
                colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(1.00f, 0.98f, 0.95f, 1.00f);
                colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.00f, 0.00f, 0.00f, 0.21f);
                colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.90f, 0.91f, 0.00f, 0.78f);
                colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.CheckMark] = new Vector4(0.25f, 1.00f, 0.00f, 0.80f);
                colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.00f, 0.00f, 0.00f, 0.14f);
                colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.Button] = new Vector4(0.00f, 0.00f, 0.00f, 0.14f);
                colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.99f, 1.00f, 0.22f, 0.86f);
                colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.Header] = new Vector4(0.25f, 1.00f, 0.00f, 0.76f);
                colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.25f, 1.00f, 0.00f, 0.86f);
                colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.Separator] = new Vector4(0.00f, 0.00f, 0.00f, 0.32f);
                colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.25f, 1.00f, 0.00f, 0.78f);
                colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.00f, 0.00f, 0.00f, 0.04f);
                colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.25f, 1.00f, 0.00f, 0.78f);
                colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.PlotLines] = new Vector4(0.40f, 0.39f, 0.38f, 0.63f);
                colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.40f, 0.39f, 0.38f, 0.63f);
                colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.25f, 1.00f, 0.00f, 1.00f);
                colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.25f, 1.00f, 0.00f, 0.43f);
                colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(1.00f, 0.98f, 0.95f, 0.73f);
            }
        }
        private static void SwitchToDark()
        {
            var style = ImGui.GetStyle();

            style.WindowMinSize = new Vector2(160.0f, 20.0f);
            style.FramePadding = new Vector2(4.0f, 2.0f);
            style.ItemSpacing = new Vector2(6.0f, 2.0f);
            style.ItemInnerSpacing = new Vector2(2.0f, 4.0f);
            style.Alpha = 0.95f;
            style.WindowRounding = 4.0f;
            style.FrameRounding = 2.0f;
            style.IndentSpacing = 6.0f;
            style.ColumnsMinSpacing = 50.0f;
            style.GrabMinSize = 14.0f;
            style.GrabRounding = 16.0f;
            style.ScrollbarSize = 12.0f;
            style.ScrollbarRounding = 16.0f;

            unsafe
            {
                var colors = style.Colors;

                colors[(int)ImGuiCol.Text] = new Vector4(0.86f, 0.93f, 0.89f, 0.78f);
                colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.86f, 0.93f, 0.89f, 0.28f);
                colors[(int)ImGuiCol.WindowBg] = new Vector4(0.13f, 0.14f, 0.17f, 1.00f);
                colors[(int)ImGuiCol.Border] = new Vector4(0.31f, 0.31f, 1.00f, 0.00f);
                colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
                colors[(int)ImGuiCol.FrameBg] = new Vector4(0.20f, 0.22f, 0.27f, 1.00f);
                colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.92f, 0.18f, 0.29f, 0.78f);
                colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.TitleBg] = new Vector4(0.20f, 0.22f, 0.27f, 1.00f);
                colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.20f, 0.22f, 0.27f, 0.75f);
                colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.20f, 0.22f, 0.27f, 0.47f);
                colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.20f, 0.22f, 0.27f, 1.00f);
                colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.09f, 0.15f, 0.16f, 1.00f);
                colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.92f, 0.18f, 0.29f, 0.78f);
                colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.CheckMark] = new Vector4(0.71f, 0.22f, 0.27f, 1.00f);
                colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.47f, 0.77f, 0.83f, 0.14f);
                colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.Button] = new Vector4(0.47f, 0.77f, 0.83f, 0.14f);
                colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.92f, 0.18f, 0.29f, 0.86f);
                colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.Header] = new Vector4(0.92f, 0.18f, 0.29f, 0.76f);
                colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.92f, 0.18f, 0.29f, 0.86f);
                colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.Separator] = new Vector4(0.14f, 0.16f, 0.19f, 1.00f);
                colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.92f, 0.18f, 0.29f, 0.78f);
                colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.47f, 0.77f, 0.83f, 0.04f);
                colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.92f, 0.18f, 0.29f, 0.78f);
                colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.PlotLines] = new Vector4(0.86f, 0.93f, 0.89f, 0.63f);
                colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.86f, 0.93f, 0.89f, 0.63f);
                colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.92f, 0.18f, 0.29f, 1.00f);
                colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.92f, 0.18f, 0.29f, 0.43f);
                colors[(int)ImGuiCol.PopupBg] = new Vector4(0.20f, 0.22f, 0.27f, 0.90f);
                colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.20f, 0.22f, 0.27f, 0.73f);
            }
        }

        static string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
    }
}