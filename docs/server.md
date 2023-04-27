# Server documentation

## OS
Roadnik Server is the .NET 6 application so it can be builded for any .NET-supported platform. Also, every release contains `ubuntu-18.04` and `windows` binaries.

## Config file
By default, server looks for `_config.json` file in __parent__ directory (../_config.json). Also, you can specify path to config by passing `-c` argument:
```bash
roadnik -c "/path/to/config.json"
```
Build-in config file is ready to be used.

Config file's options description:
```json
{
	"WebrootDirPath": "www",        // absolute or relative path to web app folder
	"LogDirPath": "logs",           // absolute or relative path to logs folder
	"DataDirPath": "data",          // absolute or relative path to storage folder
	"ThunderforestApikey": null,    // API key to https://www.thunderforest.com/ service. If this parameter is null or invalid, thunderforest maps will not work
    "PortBind": 5544,               // port of server (default: 5544)
    "IpBind": "0.0.0.0",            // ip to bind to (default: "0.0.0.0")
	"AdminApiKey": null             // admin api key (required for user management)
}
```
Roadnik Server will serve all files from config's `WebrootDirPath`. Don't place your secrets there!

## API

### HTTP GET `/store`
Stores information about geolocation
Query parameters:
```C#
string key;         // unique key
float lat;          // latitude in format xx.xx
float lon;          // longitude in format xx.xx
float alt;          // altitude in metres
float? speed;       // speed in metres per second (optional)
float? acc;         // accuracy in metres (optional)
float? battery;     // battery level in percent (optional, 0.0 - 100.0)
float? gsm_signal;  // signal in percent (optional, 0.0 - 100.0)
float? bearing;     // bearing in degrees (optional, 0 - 360)
string? var;        // user message (optional)
```

### HTTP GET `/get`
Retreives a list of geolocation points
Query parameters:
```C#
string key;         // unique key
int? limit;         // number of returned entries; new entries are returned first (optional)
long? offset;       // (optional)
```
