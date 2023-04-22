import os
import shutil
import zipfile
import build_common.packages
import build_common.git as git
import argparse
from subprocess import call

signingPassword = os.environ['ANDROID_SIGNING_KEY_PASSWORD']

sourceDirName = "Roadnik.MAUI"

argParser = argparse.ArgumentParser()
argParser.add_argument('--framework', type=str, default= "net6.0-android", required=False, help='Target framework of server')
args = argParser.parse_args()
framework: str = args.framework

outputDir = os.path.join(os.getcwd(), "output")
if (os.path.isdir(outputDir)):
    shutil.rmtree(outputDir, ignore_errors=True)

extension = ""
if (framework.endswith("android")):
    extension = "apk"

if (extension == ""):
    raise FileNotFoundError(f"Can't find pkg format for framework '{framework}'")

pkgFile = os.path.join(os.getcwd(), f"roadnik.{extension}")

if (os.path.isfile(pkgFile)):
    os.remove(pkgFile)

branch = git.get_current_branch()
commitIndex = git.get_last_commit_index()
version = f"{branch}.{commitIndex}"

print(f"===========================================", flush=True)
print(f"Output folder: '{outputDir}'", flush=True)
print(f"===========================================", flush=True)

print(f"===========================================", flush=True)
print(f"Adjusting csproj version: '{version}'", flush=True)
print(f"===========================================", flush=True)
build_common.packages.adjust_csproj_version(os.path.join(os.getcwd(), sourceDirName), version, "ApplicationDisplayVersion")
build_common.packages.adjust_csproj_version(os.path.join(os.getcwd(), sourceDirName), str(commitIndex), "ApplicationVersion")

print(f"===========================================", flush=True)
print(f"Compiling client for framework '{framework}'...", flush=True)
print(f"===========================================", flush=True)
build_common.packages.callThrowIfError(f"dotnet workload restore {sourceDirName}")
build_common.packages.callThrowIfError(f"dotnet publish {sourceDirName} -c Release -p:AndroidSigningKeyPass={signingPassword} -p:AndroidSigningStorePass={signingPassword} -f {framework} -o \"{outputDir}\"")

print(f"===========================================", flush=True)
print(f"Creating pkg '{pkgFile}'...", flush=True)
print(f"===========================================", flush=True)
for entry in os.listdir(outputDir):
    entryPath = os.path.join(outputDir, entry)
    if (os.path.isfile(entryPath) and entryPath.endswith(extension)):
        shutil.move(entryPath, pkgFile)
        break

print(f"===========================================", flush=True)
print(f"Done! Package file is '{pkgFile}'", flush=True)
print(f"===========================================", flush=True)
