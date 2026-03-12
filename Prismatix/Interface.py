print("Initializing packages...")

#Imports & DLL Load
#region
import math
import os
import sys
import clr #pythonnet, NOT colored text thing
from pathlib import Path
import time
from PIL import Image
import numpy as np
import platform

print("Architecture: ", platform.architecture())
print("Python: ", sys.executable)
print("Importing DLLs...")
dllPath = Path(__file__).parent / "bin/Debug/netstandard2.0/Prismatix.dll"
print(dllPath.exists())
print(dllPath)

clr.AddReference(str(dllPath)) #create a python lib to import
from Prismatix import Renderer
from Prismatix import Camera
from Prismatix import Config
import Prismatix.Math as PM
import Prismatix.Geometry as Geo

Config.Load(str(Path(__file__).parent / "config.json"))
#endregion


def importObject(fileName, name="UNDEFINED"):
    obj = Geo.Object(name, PM.Vector3(0,0,0), 1)
    print(f"Initializing new object...")
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
    
    print("Expected:", width * height * 3)
    print("Actual:", len(byteArrayData))

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