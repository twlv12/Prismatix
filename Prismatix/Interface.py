print("Initializing packages...")

import os
import sys
import clr #pythonnet, NOT colored text thing
from pathlib import Path

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
import Prismatix.Math as MathP
import Prismatix.Geometry as Geo


def importObject(fileName, name="UNDEFINED"):
    obj = Geo.Object(name, MathP.Vector3(0,0,0), 1)
    print(f"Initializing new object...")
    obj.LoadFromDisk(str(Path(__file__).parent / f"Geometry/{fileName}.obj"))
    print(f"Loaded {obj.name} from disk.")
    return obj

def renderImage(scene, type):
    fileOutput = f"{input('Name:')}.png"

    print("Rendering...")
    width = Config.imgWidth
    height = Config.imgHeight

    if type == "depth":
        byteArrayData = Renderer.RenderDepth(scene)
    
    data = np.frombuffer(bytearray(byteArrayData), dtype=np.uint8)
    imgArray = data.reshape((height, width, 3))
    
    image = Image.fromarray(imgArray, mode="RGB")
    image.save(fileOutput)
    
    print("Render Complete! :)")
    print(f"Saved as {fileOutput}")


scene = Geo.Scene()

cube = importObject("CubeBasic")
scene.AddObject(cube)

camera = Camera(MathP.Vector3(-2.5,-2.5, 1.5), MathP.Vector3(1,1,-0.5), MathP.Vector3(0,0,1))
scene.mainCamera = camera

renderImage(scene, "depth")