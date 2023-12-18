import os
import shutil
import zipfile
from build_common import utils
from build_common import packages
from build_common import git
import argparse

sourceDirName = "Roadnik"

argParser = argparse.ArgumentParser()
argParser.add_argument('--platform', type=str, default= "win-x64", required=False, help='Target platfrom of server')
args = argParser.parse_args()
platform = args.platform

artifactsDir = os.path.join(os.getcwd(), "artifacts")
if (not os.path.isdir(artifactsDir)):
    os.makedirs(artifactsDir)

outputDir = os.path.join(os.getcwd(), "output")
if (os.path.isdir(outputDir)):
    shutil.rmtree(outputDir, ignore_errors=True)
pkgFile = os.path.join(artifactsDir, f"server-{platform}.zip")
if (os.path.isfile(pkgFile)):
    os.remove(pkgFile)

version = f"{git.get_version_from_current_branch()}.{git.get_last_commit_index()}"

print(f"===========================================", flush=True)
print(f"Output folder: '{outputDir}'", flush=True)
print(f"===========================================", flush=True)

print(f"===========================================", flush=True)
print(f"Compiling server for platform '{platform}'...", flush=True)
print(f"Version: '{version}'", flush=True)
print(f"===========================================", flush=True)
serverOutputDir = os.path.join(outputDir, "bin")
packages.adjust_csproj_version(os.path.join(os.getcwd(), sourceDirName), version)
utils.callThrowIfError(f"dotnet publish {sourceDirName} -r {platform} --self-contained -o \"{serverOutputDir}\"", True)

print(f"===========================================", flush=True)
print(f"Compiling web...", flush=True)
print(f"===========================================", flush=True)
webSrcDir = os.path.join(os.getcwd(), "www")
webOutputDir = os.path.join(outputDir, "www")
packages.create_webpack(webSrcDir, webOutputDir)

print(f"===========================================", flush=True)
print(f"Copying sample settings...", flush=True)
print(f"===========================================", flush=True)
settingsFileSrc = os.path.join(os.getcwd(), sourceDirName, "_config.json")
settingsFileDst = os.path.join(outputDir, "_config.json")
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

git.create_tag_and_push(version, "origin", "casualshammy", True)
# git.merge("main", git.get_current_branch_name(), True, "casualshammy", True)