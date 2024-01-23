# Roadnik
## Roadnik Server
Roadnik Server is a geolocation storage server designed for programmers and IT enthusiasts. This application allows users to upload, store, and retrieve location information.
The server operates many `rooms`.  Each `room` is tied to a unique `key`, which is used to upload and download the data. `Key` may be used as a password to access location data.
Supported data fields include nicknames, latitude, longitude, altitude, speed, movement direction, geolocation accuracy, battery level, and signal strength.

As each data entry contains a `nickname` field, each `room` can contain information about multiple tracks (or users). 

Roadnik Server is fully compatible with the "Live tracking" feature of the "Locus Map" application.

It also comes with a web application in the form of an interactive map, displaying the routes in `rooms` with auto-tracking capabilities. The web application supports both OpenStreetMap (doesn't require API key) and Thunderforest (API key required) map types.

[Roadnik Server's documentation](docs/server.md)

## Roadnik App
In addition to the server, there is an Android application that enables users to send location data to the server, making it a complete client application for the Roadnik Server. With the private instance of Roadnik Server, the Roadnik App provides a comprehensive geolocation storage solution for developers and IT enthusiasts.

## Public Roadnik Server
For anyone who needs to share their location with loved ones (but doesn't want to set up their very own Roadnik Server) there is a publicly available server with the address `roadnik.app`. This server does not store any personal information (for example, IP address, device information or cookies), only location points bounded with `key` and `nickname`.

Pay attention: anyone who knows your `key` can access your location history! There are no passwords by design. Thus, use a random sequence of numbers and letters to prevent unwanted attention. For example, there are 96,717,311,574,016 unique strings of 8 alphanumeric characters in total. Roadnik Server does not allow brute-forcing `keys`, so a string of 8 random characters is safe enough to use. Roadnik App will generate a robust key for you on the first launch.