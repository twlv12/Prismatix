import os
def p(done=False):
    global c
    done = "Done!" if done else ""
    #os.system('cls')
    print("["+("-"*c)+" "*(m-c)+f"] {done}")
    c += 1
    #if done: os.system('cls')

LocalPathToDLL = "bin/Debug/netstandard2.0/Prismatix.dll"
#Imports & DLL Load
#region
print("Initializing Packages..."); c=0;m=12

import math; p()
import clr; p() #pythonnet, NOT colored text thing
from pathlib import Path; p()
import time; p()
from PIL import Image; p()
import numpy as np; p()
import sys; p()
import pygame as pg; p()
import math; p()
import threading; p()
from queue import Queue; p()
import random as r
import platform; p(done=True)

print("\nArchitecture: ", platform.architecture())
print("Python: ", sys.executable)
print("\nImporting DLLs..."); c=0; m=7
dllPath = Path(__file__).parent / LocalPathToDLL

if dllPath.exists() == False:
    print("DLL not found. Set the dll path above.")
    exit()
print(dllPath)

clr.AddReference(str(dllPath)); p() #create a python lib to import
from Prismatix import Renderer; p()
from Prismatix import Camera; p()
from Prismatix import Config; p()
import Prismatix.Math as PM; p()
import Prismatix.Geometry as Geo; p()

Config.Load(str(Path(__file__).parent / "config.json")); p(done=True)
frameTime = 10
frameTimes = []
#endregion



def importObject(fileName, name="UNDEFINED"):
    obj = Geo.Object(name, PM.Vector3(0,0,0), 1)
    try:
        obj.LoadFromDisk(str(Path(__file__).parent / f"Geometry/{fileName}"))
    except:
        print(f"Object {fileName} not found.")

    print(f"Loaded {obj.name} from disk.")
    return obj

def renderArrayToImage(scene, renderMode): #add render modes heres
    internalStartTime = time.time()
    global renderType
    if renderMode == "depth":
        byteArrayData = Renderer.RenderDepth(scene).data
    elif renderMode == "normal":
        byteArrayData = Renderer.RenderNormal(scene).data
    elif renderMode == "diffuse":
        byteArrayData = Renderer.RenderDiffuse(scene).data
    elif renderMode == "fastdiffuse":
        byteArrayData = Renderer.RenderDiffuseFast(scene).data
    else:
       print("No render mode selected, defaulting to normal.")
       renderMode, renderType = "normal", "normal"
       byteArrayData = Renderer.RenderNormal(scene).data
    
    #print("Expected:", width * height * 3)
    #print("Actual:", len(byteArrayData))

    data = np.frombuffer(byteArrayData, dtype=np.uint8)
    imgArray = data.reshape((height, width, 3))

    global frameTime
    frameTime1end = time.time()
    frameTime = round(frameTime1end-internalStartTime, 4)
    if frameTime == 0: frameTime = 1
    frameTimes.append(frameTime)

    #print(f"{frameTime} | ", end="")
    return imgArray

def worldToScreen(point):
    #transfer world coordinates to screen space
    relative = point - camera.position

    x = PM.Utils.Dot(relative, camera.right)
    y = PM.Utils.Dot(relative, camera.up)
    z = PM.Utils.Dot(relative, camera.forward)
   
    u = (x/z) / camera.vpWidth
    v = (y/z) / camera.vpHeight

    return ((u*width) + width//2, (v*height) + height//2)
#this is painful
def vectorToScreen(vector, camera):
    x = PM.Utils.Dot(vector, camera.right)
    y = PM.Utils.Dot(vector, camera.up)
    return (x*50, y*50)

def drawAxis(surface, camera):
    #gridLines = 20
    #gridSpacing = 2

    axis = { #vector3 dir and (colour in tuple)
        "x": (PM.Vector3(1,0,0), (255,0,0)),
        "y": (PM.Vector3(0,1,0), (0,255,0)),
        "z": (PM.Vector3(0,0,1), (0,0,255)),
        }

    #gridAxis = {}
    #region
    ##create grid lines
    #for i in range(-gridLines, gridLines+1):
    #    offset = i *gridSpacing
    #
    #    #two for each axis, pos and neg
    #    line1 = PM.Vector3(-gridLines*gridSpacing, offset, 0) #x
    #    line2 = PM.Vector3(gridLines*gridSpacing, offset, 0) #x
    #    line3 = PM.Vector3(offset, -gridLines*gridSpacing, 0) #y
    #    line4 = PM.Vector3(offset, gridLines*gridSpacing, 0) #y
    #
    #    for neg, pos in ((line1, line2), (line3, line4)):
    #        vecNeg = neg - camera.position
    #        negX = PM.Utils.Dot(vecNeg, camera.right)
    #        negY = PM.Utils.Dot(vecNeg, camera.up)
    #
    #        vecPos = pos - camera.position
    #        posX = PM.Utils.Dot(vecPos, camera.right)
    #        posY = PM.Utils.Dot(vecPos, camera.up)
    #
    #        pg.draw.line(surface, (50,50,50), 
    #                     (width//2 + negX*50, height//2 + negY*50),
    #                     (width//2 + posX*50, height//2 + posY*50), 1)
    #endregion
    
    #Main Axis Draw
    #region
    for axi, (vector, colour) in axis.items():
        #create a camera space vector for the axis
        cameraX = -PM.Utils.Dot(vector, camera.right)
        cameraY = -PM.Utils.Dot(vector, camera.up)
        if axi == moving:
            pg.draw.line(surface, colour, 
                         (width//2, height//2), 
                         (width//2 +cameraX*50, height//2 +cameraY*50), 12)
            pg.draw.line(surface, colour, 
                         (width//2, height//2), 
                         (width//2 +cameraX*500, height//2 +cameraY*500), 6)
        else:           
            pg.draw.line(surface, colour, 
                         (width//2, height//2), 
                         (width//2 +cameraX*50, height//2 +cameraY*50), 3)
            pg.draw.line(surface, colour, 
                         (width//2, height//2), 
                         (width//2 +cameraX*500, height//2 +cameraY*500), 1)
    #endregion

    #Object Origin Draw
    #region
    objOrigin = worldToScreen(selectedObj.position)
    for axi, (vector, colour) in axis.items():
        #second rendering for object gizmos
        vectorX, vectorY = vectorToScreen(vector, camera)
        axisVector = (objOrigin[0]+vectorX, objOrigin[1]+vectorY)
        pg.draw.line(surface, colour, objOrigin, axisVector, 2)
    #endregion
    
def averageFPS():
    #horrible, i know.
    try:
        return 1/((frameTimes[-1]+frameTimes[-2]+frameTimes[-3]+frameTimes[-4])/4)
    except:
        try:
            return 1/((frameTimes[-1]+frameTimes[-2]+frameTimes[-3])/3)
        except:
            try:
                return 1/((frameTimes[-1]+frameTimes[-2])/2)
            except:
                try:
                    return 1/frameTimes[-1]
                except:
                    return 0

def drawInfo(screen):
    lines = [
        "1: depth, 2: normal", 
        "3: diffuse, 4: fastdiffuse",
        "",
        "",
        "Left/Right arrow to orbit",
        "Up/Down arrow to zoom",
        "U/J to move cam up/down",
        "",
        "TAB to cycle selected",
        "Q to focus on selected",
        "G, x/y/z, -/+ to move object",
        "",
        f"Selected: {selectedObj.name} ({selectedIndex})",
        f"Moving: {listeningForMovement}, Axis: {moving}",
        "",
        f"X: {round(selectedObj.position.x,2)}, Y: {round(selectedObj.position.y,2)}, Z: {round(selectedObj.position.z,2)}",
        f"A: {round(angle,2)}, R: {round(radius,2)}, H: {round(height,1)}",
        "",
        f"Render Mode: {renderType}",
        f"Last: {frameTime}, FPS (avg4): {round(averageFPS(), 1)}",
        f"Verts: {numVerts}, Tris: {numTris}",
        f"Last/Tris: {round(frameTime/numTris,5)}",
        "",
        "Q - Toggle BVH overlay",
        "",
        "",
        "",
        "",
    ]

    x, y = 10, 10
    for line in lines:
        surface = font.render(line, True, (255,255,255))
        screen.blit(surface, (x, y))
        y += surface.get_height()

def traverseBVH(sf, node):
    if node.depth == bvhDepth:
        drawNodeOverlay(sf, node)
    if node.isLeaf:
        return len(node.triangles)
    else:
        return traverseBVH(sf, node.left) + traverseBVH(sf, node.right)

def drawNodeOverlay(sf, node):
    xmin = node.boundsMin.x
    xmax = node.boundsMax.x
    ymin = node.boundsMin.y
    ymax = node.boundsMax.y
    zmin = node.boundsMin.z
    zmax = node.boundsMax.z

    sV = [
        worldToScreen(PM.Vector3(xmin, ymin, zmin)),
        worldToScreen(PM.Vector3(xmax, ymin, zmin)),
        worldToScreen(PM.Vector3(xmin, ymax, zmin)),
        worldToScreen(PM.Vector3(xmax, ymax, zmin)),
        worldToScreen(PM.Vector3(xmin, ymin, zmax)),
        worldToScreen(PM.Vector3(xmax, ymin, zmax)),
        worldToScreen(PM.Vector3(xmin, ymax, zmax)),
        worldToScreen(PM.Vector3(xmax, ymax, zmax)),
    ]

    col = (
    (hash(node) & 255),
    (hash(node) >> 8) & 255,
    (hash(node) >> 16) & 255 )

    pg.draw.line(sf, col, sV[0], sV[1])
    pg.draw.line(sf, col, sV[1], sV[3])
    pg.draw.line(sf, col, sV[3], sV[2])
    pg.draw.line(sf, col, sV[2], sV[0])
    pg.draw.line(sf, col, sV[4], sV[5])
    pg.draw.line(sf, col, sV[5], sV[7])
    pg.draw.line(sf, col, sV[7], sV[6])
    pg.draw.line(sf, col, sV[6], sV[4])
    pg.draw.line(sf, col, sV[0], sV[4])
    pg.draw.line(sf, col, sV[1], sV[5])
    pg.draw.line(sf, col, sV[2], sV[6])
    pg.draw.line(sf, col, sV[3], sV[7])


#SCENE CONSTRUCTION --------------------------
scene = Geo.Scene()

#cube1 = importObject("Cube.obj")
#cube1.name = "Cube1"
#cube1.material = Geo.Material("orange", PM.Vector3(1,0.6,0.1), 1, 1)
#scene.AddObject(cube1)
#
#cube2 = importObject("Cube.obj")
#cube2.name = "Cube2"
#cube2.material = Geo.Material("blue", PM.Vector3(0.2,0.1,1), 1, 1)
#cube2.position = PM.Vector3(0,-3,0)
#scene.AddObject(cube2)
#
#sphere1 = importObject("Sphere.obj")
#sphere1.material = Geo.Material("blueMatte", PM.Vector3(1,0.2,0.1), 1, 1)
#scene.AddObject(sphere1)
#
#suzanne = importObject("Suzanne.obj")
#suzanne.material = Geo.Material("purple", PM.Vector3(0.5,0.1,0.6), 1, 1)
#scene.AddObject(suzanne)
#
#arch = importObject("Arch.obj")
#arch.material = Geo.Material("blue", PM.Vector3(0.1,0.1,0.7), 1, 1)
#scene.AddObject(arch)
#
fighter = importObject("Fighter.obj")
fighter.material = Geo.Material("white", PM.Vector3(0.85,0.85,1), 1, 1)
scene.AddObject(fighter)
#
#oven = importObject("Oven.obj")
#oven.material = Geo.Material("red", PM.Vector3(0.85,0.2,0.1), 1, 1)
#scene.AddObject(oven)
#
#Fan = importObject("Fan.obj")
#Fan.material = Geo.Material("white", PM.Vector3(1,1,1), 1, 1)
#scene.AddObject(Fan)










camera = Camera(PM.Vector3(0,0,0), PM.Vector3(1,0,0), PM.Vector3(0,0,1))
scene.mainCamera = camera
radius = 5

lamp = Geo.Lamp(PM.Vector3(1.8,-2,-1.8), 2000)
scene.AddLamp(lamp)

renderType = input("\n\nRender depth/normal/diffuse : ").lower()
#\SCENE CONSTRUCTION -------------------------


#Pygame Initialization
#region
print("Initializing 3D Editor...")
pg.init()
width = Config.imgWidth
height = Config.imgHeight

sceen = pg.display.set_mode((width, height))
pg.display.set_caption("Prismatix")
clock = pg.time.Clock()
font = pg.font.Font(r"C:\Users\ethan\source\repos\twlv12\Prismatix\Prismatix\font.ttf", 16)
print("Screen OK...")

numVerts = 0
numTris = 0
for obj in scene.objects:
    numVerts += len(obj.mesh.vertices)
    numTris += len(obj.mesh.indices)//3
print(f"Vertices: {numVerts}, Triangles: {numTris}")

running = True
rendering = True
surface = None
angle = -2.4
camHeight = -3
listeningForMovement = False
moving = None
vector = PM.Vector3(0,0,0)
focused = PM.Vector3(0,0,0)
renderingBVH = False;
bvhDepth = 0

allSceneObjects = []
for obj in scene.objects: allSceneObjects.append(obj)
for lamp in scene.lamps: allSceneObjects.append(lamp)
for allObj in allSceneObjects: print(f"Obj: {allObj.name}")

selectedIndex = 0
selectedObj = allSceneObjects[selectedIndex]

drawInfo(sceen)
print("\nReady!")
#endregion

while running:
    #KEYBINDS
    for event in pg.event.get():
        if event.type == pg.QUIT:
            running = False
        if event.type == pg.KEYDOWN:
            if event.key == pg.K_LEFT:
                angle += 0.2
            if event.key == pg.K_RIGHT:
                angle -= 0.2
            if event.key == pg.K_UP:
                if radius > 1:
                    radius -= 1
            if event.key == pg.K_DOWN:
                radius += 1
            if event.key == pg.K_u:
                camHeight -= 1
            if event.key == pg.K_j:
                camHeight += 1
            if event.key == pg.K_q:
                focused = selectedObj.position

            if event.key == pg.K_1:
                renderType = "depth"
            if event.key == pg.K_2:
                renderType = "normal"
            if event.key == pg.K_3:
                renderType = "diffuse"
            if event.key == pg.K_4:
                renderType = "fastdiffuse"
            if event.key == pg.K_b:
                renderingBVH = not renderingBVH
            if event.key == pg.K_LEFTBRACKET:
                bvhDepth -= 1
            if event.key == pg.K_RIGHTBRACKET:
                bvhDepth += 1

            if event.key == pg.K_TAB:
                selectedIndex = (selectedIndex +1) % len(allSceneObjects)
                selectedObj = allSceneObjects[selectedIndex]

            if event.key == pg.K_g:
                listeningForMovement = not listeningForMovement
                moving = None
            if listeningForMovement:
                if event.key == pg.K_x:
                    moving = "x"
                    vector = PM.Vector3(0.2,0,0)
                if event.key == pg.K_y:
                    moving = "y"
                    vector = PM.Vector3(0,0.2,0)
                if event.key == pg.K_z:
                    moving = "z"
                    vector = PM.Vector3(0,0,0.2)
                if event.key == pg.K_EQUALS:
                    selectedObj.position = selectedObj.position - vector
                    selectedObj.needsPrecomp = True
                if event.key == pg.K_MINUS:
                    selectedObj.position = selectedObj.position + vector
                    selectedObj.needsPrecomp = True

            rendering = True
    
    if rendering:
        x = (radius * math.cos(angle)) + focused.x
        y = (radius * math.sin(angle)) + focused.y
        camera.position = PM.Vector3(x,y,camHeight)
        camera.RotateTo(focused)

        imgArray = renderArrayToImage(scene, renderType)
        surface = pg.surfarray.make_surface(np.transpose(imgArray, (1, 0, 2)))
        rendering = False

    if surface != None:
        sceen.blit(surface, (0,0))
        drawAxis(sceen, camera)
        drawInfo(sceen)
        if renderingBVH:
            traverseBVH(sceen, scene.rootBVH)

    pg.display.flip()
    clock.tick(60)

print("\nShutting Down...")