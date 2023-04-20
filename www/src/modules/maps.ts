
import * as L from "leaflet"

export function GetMapLayers()
{
	// OpenStreetMap
	var osmUrl = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
		osmAttribution = 'Map Data from <a href="http://www.openstreetmap.org/" target="_blank">OpenStreetMap</a> (<a href="http://creativecommons.org/licenses/by-sa/2.0/" target="_blank">CC-by-SA 2.0</a>)',
		osm = new L.TileLayer(osmUrl, {maxZoom: 18, attribution: osmAttribution});
	// OpenCycleMap
	var cyclemapUrl = '/thunderforest?type=cycle&x={x}&y={y}&z={z}',
		cyclemap = new L.TileLayer(cyclemapUrl, {maxZoom: 18, attribution: undefined});
	// Spinal Map
	var spinalmapUrl = '/thunderforest?type=spinal-map&x={x}&y={y}&z={z}',
		spinalmap = new L.TileLayer(spinalmapUrl, {maxZoom: 18, attribution: undefined});
	// Googly Hybrid
	var googleUrl = "https://mts.google.com/vt/lyrs=y&x={x}&y={y}&z={z}",
		googleAttribution = "(c)2015 Google - Map data",
		google = new L.TileLayer(googleUrl, {maxZoom: 28, attribution: googleAttribution});

	var baseMaps = {
		"OpenStreetMap": osm,
		"OpenCycleMap": cyclemap,
		"Spinal Map": spinalmap,
		"Google Hybrid": google,
	};
	
    return baseMaps;
}

export function GetMapOverlayLayers() {
    // Waymarked
    var waymarkedshadinghikeUrl = 'http://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png',
    waymarkedshadinghikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
    waymarkedshadinghike = new L.TileLayer(waymarkedshadinghikeUrl, {maxZoom: 18, attribution: waymarkedshadinghikeAttribution});
    // Waymarked
    var waymarkedshadingbikeUrl = 'http://tile.waymarkedtrails.org/cycling/{z}/{x}/{y}.png',
    waymarkedshadingbikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
    waymarkedshadingbike = new L.TileLayer(waymarkedshadingbikeUrl, {maxZoom: 18, attribution: waymarkedshadingbikeAttribution});

    var overlayMaps = {
		"Trails": waymarkedshadinghike,
		"Radrouten": waymarkedshadingbike,
	};

    return overlayMaps;
}