import os
import clr #pythonnet, NOT colored text thing
from pathlib import Path

from PIL import Image
import numpy as np

print("Importing DLLs...")
dllPath = Path(__file__).parent / "bin/Debug/netstandard2.0/Prismatix.dll"
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

def renderImage(scene):
    fileOutput = f"{input("Name:")}.png"

    print("Rendering...")
    width = Config.imgWidth
    height = Config.imgHeight
    byteArrayData = Renderer.Render(scene)
    
    data = np.frombuffer(bytearray(byteArrayData), dtype=np.uint8)
    imgArray = data.reshape((height, width, 3))
    
    image = Image.fromarray(imgArray, mode="RGB")
    image.save(fileOutput)
    
    print("Render Complete! :)")
    print(f"Saved as {fileOutput}")


scene = Geo.Scene()

cube = importObject("CubeBasic")
scene.AddObject(cube)

camera = Camera(MathP.Vector3(0,-15,0), MathP.Vector3(0,1,0), MathP.Vector3(0,0,1))
scene.mainCamera = camera

renderImage(scene)