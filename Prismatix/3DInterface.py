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
print("Initializing Packages..."); c=0;m=9

import math; p()
import clr; p() #pythonnet, NOT colored text thing
from pathlib import Path; p()
import time; p()
from PIL import Image; p()
import numpy as np; p()
import sys; p()
import pygame as pg; p()
import math; p()
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
def renderImageToSurface(scene, type):
    startTime = time.time() #add render modes heres
    if type == "depth":
        byteArrayData = Renderer.RenderDepth(scene).data
    if type == "normal":
        byteArrayData = Renderer.RenderNormal(scene).data
    
    #print("Expected:", width * height * 3)
    #print("Actual:", len(byteArrayData))

    data = np.frombuffer(bytearray(byteArrayData), dtype=np.uint8)
    imgArray = data.reshape((height, width, 3))

    endTime = time.time()
    global frameTime
    frameTime = round(endTime-startTime, 2)
    if frameTime == 0: frameTime = 10

    return pg.surfarray.make_surface(np.transpose(imgArray, (1, 0, 2)))

def drawAxis(surface, camera):
    gridLines = 20
    gridSpacing = 0.5

    axis = { #vector and colour in tuple
        "x": (PM.Vector3(1,0,0), (255,0,0)),
        "y": (PM.Vector3(0,1,0), (0,255,0)),
        "z": (PM.Vector3(0,0,1), (0,0,255)),
        }
    #gridAxis = {}
    #
    ##create grid lines
    #for i in range(-gridLines, gridLines+1):
    #    offset = i *gridSpacing
    #
    #    line1 = PM.Vector3(-gridLines*gridSpacing, offset, 0) #x
    #    line2 = PM.Vector3(gridLines*gridSpacing, offset, 0) #x
    #    line3 = PM.Vector3(offset, -gridLines*gridSpacing, 0) #y
    #    line4 = PM.Vector3(offset, gridLines*gridSpacing, 0) #y


    
    for axi, (vector, colour) in axis.items():
        #create a camera space vector for the axis
        cameraX = -PM.Utils.Dot(vector, camera.right)
        cameraY = -PM.Utils.Dot(vector, camera.up)
        if axi == moving:
            pg.draw.line(surface, colour, (width//2, height//2), (width//2 +cameraX*50, height//2 +cameraY*50), 12)
        else:           
            pg.draw.line(surface, colour, (width//2, height//2), (width//2 +cameraX*50, height//2 +cameraY*50), 3)
        if axi == moving:
            pg.draw.line(surface, colour, (width//2, height//2), (width//2 +cameraX*500, height//2 +cameraY*500), 6)
        else:
            pg.draw.line(surface, colour, (width//2, height//2), (width//2 +cameraX*500, height//2 +cameraY*500), 1)

    #for gridaxi, (vector, colour) in gridAxis.items():
    #    cameraX = -PM.Utils.Dot(vector, camera.right)
    #    cameraY = -PM.Utils.Dot(vector, camera.up)
    #    pg.draw.line(surface, colour, (width//2 +cameraX*500, height//2 +cameraY*500), (width//2 -cameraX*500, height//2 -cameraY*500), 1)

def drawInfo(screen):
    lines = [
        "left/right Arrow to orbit",
        "up/down Arrow to zoom",
        "G, x/y/z, -/+ to move object",
        "",
        f"Moving: {listeningForMovement}, Axis: {moving}",
        "",
        f"Frametime: {frameTime}, FPS: {round(1/frameTime, 1)} (est)",
        f"Verts: {numVerts}, Tris: {numTris}"
        "",
        "",
    ]

    x, y = 10, 10
    for line in lines:
        surface = font.render(line, False, (255,255,255))
        screen.blit(surface, (x, y))
        y += surface.get_height()


#SCENE CONSTRUCTION --------------------------
scene = Geo.Scene()

cube = importObject("Cube")
scene.AddObject(cube)
selectedObj = cube

camera = Camera(PM.Vector3(0,0,0), PM.Vector3(0,0,0), PM.Vector3(0,0,1))
scene.mainCamera = camera
radius = 5

renderType = input("\n\nRender depth/normal : ").lower()
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
font = pg.font.Font("font.ttf", 16)

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

        surface = renderImageToSurface(scene, renderType)
        rendering = False

    if surface != None:
        sceen.blit(surface, (0,0))
        drawAxis(sceen, camera)
        drawInfo(sceen)

    pg.display.flip()
    clock.tick(60)