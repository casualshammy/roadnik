import os
import argparse
from build_common import git, docker, packages, utils

sourceDirName = "Roadnik"

dockerRepo = os.getenv('DOCKER_REPO')
dockerLogin = os.getenv('DOCKER_LOGIN')
dockerPassword = os.getenv('DOCKER_PASSWORD')

argParser = argparse.ArgumentParser()
argParser.add_argument('--platform', type=str, default= "linux-amd64", required = False)
args = argParser.parse_args()
platform: str = args.platform

version = f"{git.get_version_from_current_branch()}.{git.get_last_commit_index()}"

print(f"===========================================", flush=True)
print(f"Creating docker image...", flush=True)
print(f"Version: '{version}'", flush=True)
print(f"===========================================", flush=True)
packages.adjust_csproj_version(os.path.join(os.getcwd(), sourceDirName), version)
if (platform == "linux-arm64"):
  packages.csproj_switch_aot(os.path.join(os.getcwd(), sourceDirName), False)

docker.buildPush(f"{dockerRepo}:{version}-{platform}", f"Roadnik/Dockerfile.{platform}", dockerLogin, dockerPassword)
docker.buildPush(f"{dockerRepo}:latest-{platform}", f"Roadnik/Dockerfile.{platform}", dockerLogin, dockerPassword)

print(f"===========================================", flush=True)
print(f"Done!", flush=True)
print(f"===========================================", flush=True)

git.create_tag_and_push(version, "origin", "casualshammy", True)
utils.callThrowIfError("git stash", True)
git.merge("main", git.get_current_branch_name(), True, "casualshammy", True)