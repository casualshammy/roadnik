import * as L from "leaflet"
import * as Api from "./modules/api";
import * as Maps from "./modules/maps"
import { TimeSpan } from "./modules/timespan";

const p_storageApi = new Api.StorageApi();

async function refreshPosition(_key: string) {
    //debugger;
    const data = await p_storageApi.getDataAsync(_key);
    if (data === null || !data.Success)
        return;

    const latLng = new L.LatLng(data.LastEntry!.Latitude, data.LastEntry!.Longitude, data.LastEntry!.Altitude);
    marker.setLatLng(latLng);
    circle.setLatLng(latLng);
    circle.setRadius(data.LastEntry!.Accuracy ?? 100);

    if (!p_firstCentered) {
        map.setView(latLng, 15);
        p_firstCentered = true;
    }

    // time since last update
    const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - data.LastUpdateUnixMs!);

    // speed
    const kmh = (data.LastEntry!.Speed ?? 0) * 3.6;

    // altitude change
    let altChangeMark = "\u2192";
    if (data.LastEntry!.Altitude > p_lastAlt) {
        altChangeMark = "\u2191";
    };
    if (data.LastEntry!.Altitude < p_lastAlt) {
        altChangeMark = "\u2193";
    }
    p_lastAlt = data.LastEntry!.Altitude;
    
    // update popup
    const popUpText = 
        `<b>${key}</b>: ${data.LastEntry!.Message ?? "Hi!"}
        </br>
        <p>
        Speed: ${kmh.toFixed(2)} km/h
        </br>
        Altitude ( ${altChangeMark} ): ${data.LastEntry!.Altitude} m
        </br>
        Heading: ${data.LastEntry!.Bearing} degrees
        </p>
        <p>
        Battery: ${data.LastEntry!.Battery}%
        </br>
        GSM power: ${data.LastEntry!.GsmSignal}%
        <br/>
        Updated ${elapsedSinceLastUpdate.toString(false)} ago
        </p>`;
    marker.setPopupContent(popUpText);

    if (p_autoPan === true) {
        map.panTo(latLng);
    }
    
    const points = data.Entries.map(_x => new L.LatLng(_x.Latitude, _x.Longitude, _x.Altitude));
    path.setLatLngs(points);
}

const queryString = window.location.search;
const urlParams = new URLSearchParams(queryString);
const key = urlParams.get('key');

let p_lastAlt = 0;
let p_firstCentered = false;
let p_autoPan = false;

const maps = Maps.GetMapLayers();
const overlays = Maps.GetMapOverlayLayers();

var map = new L.Map('map', {
    center: new L.LatLng(51.4768, 0.0006),
    zoom: 14,
    layers: [maps.OpenStreetMap]
});

const layersControl = new L.Control.Layers(maps, overlays);
map.addControl(layersControl);

const marker = L.marker([51.4768, 0.0006])
    .addTo(map)
    .bindPopup("<b>Connection lost!</b>")
    .openPopup();

const circle = L.circle([51.4768, 0.0006], 100, { color: 'blue', fillColor: '#f03', fillOpacity: 0.5 })
    .addTo(map);

const path = L.polyline([], {color: 'red', smoothFactor: 1, weight: 5})
    .addTo(map);

if (key !== null) {
    const ws = p_storageApi.setupWs(key, (_ws, _data) => {
        if (_data.Type === Api.WS_MSG_TYPE_DATA_UPDATED || _data.Type === Api.WS_MSG_TYPE_HELLO)
            refreshPosition(key);
    });
}