
setlocal

cd ..\www-vue
CALL npm i
CALL npm run build

rd /s /q ..\Roadnik.MAUI\Resources\Raw\webApp
mkdir ..\Roadnik.MAUI\Resources\Raw\webApp
xcopy dist\room\* ..\Roadnik.MAUI\Resources\Raw\webApp /E /I /Y

endlocal