import os
import clr #pythonnet, NOT colored text thing
from pathlib import Path

from PIL import Image
import numpy as np


fileOutput = f"{input("Name:")}.png"


print("Importing DLL...")
dllPath = Path(__file__).parent / "bin/Debug/netstandard2.0/Prismatix.dll"
clr.AddReference(str(dllPath)) #create a python lib to import
from Prismatix import Renderer
from Prismatix import Config


print("Rendering...")
width = Config.imgWidth
height = Config.imgHeight
byteArrayData = Renderer.Render()


data = np.frombuffer(bytearray(byteArrayData), dtype=np.uint8)
imgArray = data.reshape((height, width, 3))

image = Image.fromarray(imgArray, mode="RGB")
image.save(fileOutput)

print("Render Complete! :)")
print(f"Saved as {fileOutput}")