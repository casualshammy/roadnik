import os
import shutil
import zipfile
import build_common.packages
import build_common.git as git
import argparse
from subprocess import call

sourceDirName = "Roadnik"

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

branch = git.get_current_branch()
commitIndex = git.get_last_commit_index()
version = f"{branch}.{commitIndex}"

print(f"===========================================", flush=True)
print(f"Output folder: '{outputDir}'", flush=True)
print(f"===========================================", flush=True)

print(f"===========================================", flush=True)
print(f"Compiling server for platform '{platform}'...", flush=True)
print(f"Version: '{version}'", flush=True)
print(f"===========================================", flush=True)
serverOutputDir = os.path.join(outputDir, "bin")
build_common.packages.adjust_csproj_version(os.path.join(os.getcwd(), sourceDirName), version)
build_common.packages.callThrowIfError(f"dotnet build {sourceDirName} -c release -r {platform} --self-contained -o \"{serverOutputDir}\"")

print(f"===========================================", flush=True)
print(f"Compiling web...", flush=True)
print(f"===========================================", flush=True)
webSrcDir = os.path.join(os.getcwd(), "www")
webOutputDir = os.path.join(outputDir, "www")
build_common.packages.create_webpack(webSrcDir, webOutputDir)

print(f"===========================================", flush=True)
print(f"Copying sample settings...", flush=True)
print(f"===========================================", flush=True)
settingsFileSrc = os.path.join(os.getcwd(), sourceDirName, "_settings.json")
settingsFileDst = os.path.join(outputDir, "_settings.json")
shutil.copy(settingsFileSrc, settingsFileDst)

print(f"===========================================", flush=True)
print(f"Creating pkg...", flush=True)
print(f"===========================================", flush=True)
with zipfile.ZipFile(pkgFile, 'w', zipfile.ZIP_DEFLATED) as pkgZipFile:
    for root, _, files in os.walk(outputDir):
        for file in files:
            filePath = os.path.join(root, file)
            pkgZipFile.write(filePath, os.path.relpath(filePath, outputDir))

print(f"===========================================", flush=True)
print(f"Done! Package file is '{pkgFile}'", flush=True)
print(f"===========================================", flush=True)

os.environ["TAG_VERSION"] = version