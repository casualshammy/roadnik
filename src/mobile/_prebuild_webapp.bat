
setlocal

cd ..\frontend\app
CALL npm i
CALL npm run build

rd /s /q ..\..\mobile\Resources\Raw\webApp
mkdir ..\..\mobile\Resources\Raw\webApp
xcopy dist\room\* ..\..\mobile\Resources\Raw\webApp /E /I /Y

endlocal