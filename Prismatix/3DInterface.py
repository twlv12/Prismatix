import os
from selectors import SelectorKey
from turtle import position
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
print("Initializing Packages..."); c=0;m=11

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
#endregion


def importObject(fileName, name="UNDEFINED"):
    obj = Geo.Object(name, PM.Vector3(0,0,0), 1)
    obj.LoadFromDisk(str(Path(__file__).parent / f"Geometry/{fileName}.obj"))
    print(f"Loaded {obj.name} from disk.")
    return obj

frameTime = 10
def threadArrayToSurface(scene,mode="normal"):
    t = threading.Thread(target=render, args=(scene,), kwargs={'rMode': mode})
    t.start()

q = Queue()
def render(scene, rMode):
    global q
    q.put(prismatRender(scene, renderMode=rMode))

def prismatRender(scene, renderMode): #add render modes heres
    internalStartTime = time.time()
    if renderMode == "depth":
        byteArrayData = Renderer.RenderDepth(scene).data
    elif renderMode == "normal":
        byteArrayData = Renderer.RenderNormal(scene).data
    elif renderMode == "diffuse":
        byteArrayData = Renderer.RenderDiffuse(scene).data
    elif renderMode == "fastdiffuse":
        byteArrayData = Renderer.RenderDiffuseFast(scene).data
    else: print("No render mode!")
    frameTime1end = time.time()
    
    #print("Expected:", width * height * 3)
    #print("Actual:", len(byteArrayData))

    data = np.frombuffer(byteArrayData, dtype=np.uint8)
    imgArray = data.reshape((height, width, 3))

    internalframeTime = round(frameTime1end-internalStartTime, 2)
    if internalframeTime == 0: internalframeTime = 1
    print(f"Internal: {internalframeTime}")
    return imgArray

def worldToScreenCoords(point, camera):
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
    gridLines = 20
    gridSpacing = 2

    axis = { #vector and colour in tuple
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
    
    objOrigin = worldToScreenCoords(selectedObj.position, camera)
    for axi, (vector, colour) in axis.items():
        #second rendering for object gizmos
        vectorX, vectorY = vectorToScreen(vector, camera)
        axisVector = (objOrigin[0]+vectorX, objOrigin[1]+vectorY)
        pg.draw.line(surface, colour, objOrigin, axisVector, 2)

def drawInfo(screen):
    lines = [
        "n: normal, d: depth, i: diffuse",
        "left/right Arrow to orbit",
        "left/right Arrow to orbit",
        "up/down Arrow to zoom",
        "G, x/y/z, -/+ to move object",
        "",
        f"Selected: {selectedObj.name} ({selectedIndex})",
        f"Moving: {listeningForMovement}, Axis: {moving}",
        f"X: {round(selectedObj.position.x,2)}, Y: {round(selectedObj.position.y,2)}, Z: {round(selectedObj.position.z,2)}",
        "",
        f"Frametime: {frameTime}, FPS: {round(1/frameTime, 1)} (est)",
        f"Verts: {numVerts}, Tris: {numTris}"
        "",
        "",
    ]

    x, y = 10, 10
    for line in lines:
        surface = font.render(line, True, (255,255,255))
        screen.blit(surface, (x, y))
        y += surface.get_height()


#SCENE CONSTRUCTION --------------------------
scene = Geo.Scene()

obj = importObject("Sphere")
scene.AddObject(obj)

camera = Camera(PM.Vector3(0,0,0), PM.Vector3(0,0,0), PM.Vector3(0,0,1))
scene.mainCamera = camera
radius = 5

lamp = Geo.Lamp(PM.Vector3(1.8,-2,-1.8), 800)
scene.AddLamp(lamp)

renderType = input("\n\nRender depth/normal/diffuse : ").lower()
#\SCENE CONSTRUCTION -------------------------


#Pygame Initialization
#region
print("Initializing 3D Viewer...")
pg.init()
width = Config.imgWidth
height = Config.imgHeight

sceen = pg.display.set_mode((width, height))
pg.display.set_caption("Prismatix")
clock = pg.time.Clock()
font = pg.font.Font(r"C:\Users\ethan\source\repos\twlv12\Prismatix\Prismatix\font.ttf", 16)

numVerts = 0
numTris = 0
for obj in scene.objects:
    numVerts += len(obj.mesh.vertices)
    numTris += len(obj.mesh.vertices)//3

running = True
rendering = True
surface = None
angle = 0
listeningForMovement = False
moving = None
vector = PM.Vector3(0,0,0)

allSceneObjects = []
for obj in scene.objects: allSceneObjects.append(obj)
for lamp in scene.lamps: allSceneObjects.append(lamp)

selectedIndex = 0
selectedObj = allSceneObjects[selectedIndex]

drawInfo(sceen)
#endregion

while running:
    for event in pg.event.get():
        if event.type == pg.QUIT:
            running = False
        if event.type == pg.KEYDOWN:
            if event.key == pg.K_LEFT:
                angle -= 0.2
            if event.key == pg.K_RIGHT:
                angle += 0.2
            if event.key == pg.K_UP:
                radius -= 1
            if event.key == pg.K_DOWN:
                radius += 1

            if event.key == pg.K_d:
                renderType = "depth"
            if event.key == pg.K_n:
                renderType = "normal"
            if event.key == pg.K_i:
                renderType = "diffuse"
            if event.key == pg.K_f:
                renderType = "fastdiffuse"

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
                if event.key == pg.K_MINUS:
                    selectedObj.position = selectedObj.position + vector

            rendering = True
    
    if rendering:
        x = radius * math.cos(angle)
        y = radius * math.sin(angle)
        camera.position = PM.Vector3(x,y,-3)
        camera.RotateTo(PM.Vector3(0,0,0))

        threadArrayToSurface(scene, mode=renderType)
        rendering = False

    if not q.empty():
        imgArray = q.get()
        surface = pg.surfarray.make_surface(np.transpose(imgArray, (1, 0, 2)))
        rendering = False

        sceen.blit(surface, (0,0))
        drawAxis(sceen, camera)
        drawInfo(sceen)

    pg.display.flip()
    clock.tick(60)