# Roadnik
## Roadnik Server
Introducing Roadnik Server: a geolocation storage server designed for programmers and IT enthusiasts. This powerful application allows users to upload, store, and retrieve location information for any person or object. Each set of coordinates is tied to a unique `key`, which is used to upload and download the data. Supported data includes latitude, longitude, altitude, speed, movement direction, geolocation accuracy, battery level, signal strength, and customizable user messages. Each data entry is individually tied to a specific geolocation.

Roadnik Server is fully compatible with the "Live tracking" feature of the "Locus Map" application. It also comes with a web application in the form of an interactive map, displaying the route of the associated unique `key` with auto-tracking capabilities. The web application supports both OpenStreetMap (doesn't require API key) and Thunderforest (API key required) map types.

[Roadnik Server's documentation](docs/server.md)

## Roadnik App
In addition to the server, there is an Android application called that enables users to send location data to the server, making it a complete client application for the Roadnik Server. With private instance of Roadnik Server, Roadnik App provides a comprehensive geolocation storage solution for developers and IT enthusiasts alike.

## Public Roadnik Server
For anyone who needs to share their location with loved ones there is a publicly available server with address `*.org`. This server does not store any personal information (for example, IP address, device information or cookies), only location points bounded with `key`.
Pay attention: anyone who knows your `key` can access your locations history! There are not passwords by design. Thus, use random sequence of numbers and letters to prevent unwanted attention. For example, there are 96,717,311,574,016 unique strings of 8 alphanumeric characters in total. Roadnik Server does not allow brute-forcing `keys`, so a string of 8 random characters is safe enough to use. Roadnik App will generate a robust key for you on the first launch.