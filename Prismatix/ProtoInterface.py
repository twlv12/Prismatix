import os
def p(done=False):
    global c
    done = "Done!" if done else ""
    os.system('cls')
    print(current+("-"*c)+" "*(m-c)+f"] {done}")
    c += 1

LocalPathToDLL = "bin/Debug/netstandard2.0/Prismatix.dll"

#Imports & DLL Load
#region
current="Initializing packages... ["; c=0; m = 7

import math; p()
import clr; p() #pythonnet, NOT colored text thing
from pathlib import Path; p()
import time; p()
from PIL import Image; p()
import numpy as np; p()
import sys; p()
import platform; p(done=True)

print("\nArchitecture: ", platform.architecture())
print("Python: ", sys.executable)
print("Importing DLLs..."); c=0; m = 8
dllPath = Path(__file__).parent / LocalPathToDLL; p()

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

def renderImage(scene, type):
    fileOutput = f"{input('Name:')}.png"

    print("Rendering...")
    width = Config.imgWidth
    height = Config.imgHeight

    startTime = time.time() #add render modes heres
    if type == "depth":
        byteArrayData = Renderer.RenderDepth(scene).data
    if type == "normal":
        byteArrayData = Renderer.RenderNormal(scene).data
    endTime = time.time()
    
    #print("Expected:", width * height * 3)
    #print("Actual:", len(byteArrayData))

    data = np.frombuffer(bytearray(byteArrayData), dtype=np.uint8)
    imgArray = data.reshape((height, width, 3))
    
    image = Image.fromarray(imgArray, mode="RGB")
    image.save(fileOutput)
    
    print("Render Complete! :)")
    print(f"Saved as {fileOutput}")
    print(f"Took {round(endTime-startTime, 2)} seconds to render")


scene = Geo.Scene()
cube = importObject("CubeBasic")
scene.AddObject(cube)

camera = Camera(PM.Vector3(-5,-4, -3), PM.Vector3(0,0,0), PM.Vector3(0,0,1))
scene.mainCamera = camera
scene.mainCamera.RotateTo(PM.Vector3(0,0,0))

renderImage(scene, "normal")