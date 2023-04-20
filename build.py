import os
import shutil
import zipfile
import build_common.packages
import argparse
from subprocess import call

argParser = argparse.ArgumentParser()
argParser.add_argument('--platform', type=str, default= "win-x64", required=False, help='Target platfrom of server')
args = argParser.parse_args()
platform = args.platform

outputDir = os.path.join(os.getcwd(), "output")
if (os.path.isdir(outputDir)):
    shutil.rmtree(outputDir, ignore_errors=True)
pkgFile = os.path.join(os.getcwd(), f"server-{platform}.zip")
if (os.path.isfile(pkgFile)):
    os.remove(pkgFile)

print(f"===========================================", flush=True)
print(f"Output folder: '{outputDir}'", flush=True)
print(f"===========================================", flush=True)

print(f"===========================================", flush=True)
print(f"Compiling server for platform '{platform}'...", flush=True)
print(f"===========================================", flush=True)
serverOutputDir = os.path.join(outputDir, "bin")
call(f"dotnet build Roadnik -c release -r {platform} --self-contained -o \"{serverOutputDir}\"")

print(f"===========================================", flush=True)
print(f"Compiling web...", flush=True)
print(f"===========================================", flush=True)
webSrcDir = os.path.join(os.getcwd(), "www")
webOutputDir = os.path.join(outputDir, "www")
build_common.packages.create_webpack(webSrcDir, webOutputDir)

print(f"===========================================", flush=True)
print(f"Copying sample settings...", flush=True)
print(f"===========================================", flush=True)
settingsFileSrc = os.path.join(os.getcwd(), "_settings.json")
settingsFileDst = os.path.join(outputDir, "_settings.json")
shutil.move(settingsFileSrc, settingsFileDst)

print(f"===========================================", flush=True)
print(f"Creating pkg...", flush=True)
print(f"===========================================", flush=True)
with zipfile.ZipFile(pkgFile, 'w', zipfile.ZIP_DEFLATED) as pkgFile:
    for root, _, files in os.walk(outputDir):
        for file in files:
            filePath = os.path.join(root, file)
            pkgFile.write(filePath, os.path.relpath(filePath, outputDir))

print(f"===========================================", flush=True)
print(f"Done! Package file is '{pkgFile}'", flush=True)
print(f"===========================================", flush=True)
