# Server documentation

## OS
Roadnik Server is the .NET 8 application so it can be builded for any .NET-supported platform. Also, every github release contains `linux-x64` and `windows-x64` binaries.

## Config file
By default, server looks for `_config.json` file in __parent__ directory (../_config.json). Also, you can specify path to config by passing `-c` argument:
```bash
roadnik -c "/path/to/config.json"
```
Or you can specify path to config file using enviroment variable `ROADNIK_CONFIG`.
Config file in distributive package (github release) is ready to be used as is.

Config file's options description:
```json
{
	"WebrootDirPath": "www",				// absolute or relative path to web app folder
	"LogDirPath": "logs",					// absolute or relative path to logs folder
	"DataDirPath": "data",					// absolute or relative path to storage folder
	"ThunderforestApikey": null,			// API key to https://www.thunderforest.com/ service. If this parameter is null or invalid, thunderforest maps will not work
	"ThunderforestCacheSize": 0,			// size in bytes of local cache of thunderforest tiles (default: 0)
	"PortBind": 5544,						// port of server (default: 5544)
	"IpBind": "0.0.0.0",					// ip to bind to (default: "0.0.0.0")
	"AdminApiKey": null,					// admin api key (required for user management)
	"AllowAnonymousPublish": true,			// allows unregistered users to publish geolocation data (**default: true**)
	"AnonymousMaxPoints": 100,				// max geolocation points to store for unregistered users (default: 100); old point will be purged first
	"RegisteredMaxPoints": 1000,			// max geolocation points to store for registered users (default: 1000); old point will be purged first
	"AnonymousMinIntervalMs": 9900,			// minimum allowed interval between storage requests for unregistered users (default: 10 sec)
	"RegisteredMinIntervalMs": 900,			// minimum allowed interval between storage requests for registered users (default: 1 sec)
}
```
Roadnik Server will serve all files from config's `WebrootDirPath`. Don't place your secrets there!

## Using as service behind NGINX
It is possible to use nginx as reverse proxy for Roadnik Server (for example, for SSL support). Nginx config example:
```
server {
	listen 					443 ssl;
	server_name 			awesome.domain.com;
	ssl_certificate     	/etc/letsencrypt/live/awesome.domain.com/fullchain.pem;
	ssl_certificate_key 	/etc/letsencrypt/live/awesome.domain.com/privkey.pem;
	index 					index.html index.htm index.php;

	location /roadnik/ {
		proxy_pass http://127.0.0.1:5544/;
		
		proxy_set_header Host $host;
		proxy_set_header X-Real-IP $remote_addr;
		proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
		
		proxy_http_version 1.1;
		proxy_set_header Upgrade    	$http_upgrade;
		proxy_set_header Connection 	"upgrade";
	}
}
```

## API

### HTTP GET `/store-path-point`
Stores information about geolocation
Query parameters:
```C#
string roomId;      // room id
string username;	// username
float lat;          // latitude in format xx.xx
float lng;          // longitude in format xx.xx
float alt;          // altitude in metres
float? speed;       // speed in metres per second (optional)
float? acc;         // accuracy in metres (optional)
float? battery;     // battery level in percent (optional, 0.0 - 1.0)
float? gsmSignal;   // signal in percent (optional, 0.0 - 1.0)
float? bearing;     // bearing in degrees (optional, 0 - 360)
```

### HTTP GET `/get`
Retreives a list of geolocation points
Query parameters:
```C#
string roomId;      // room id
long? offset;       // timestamp offset
```
