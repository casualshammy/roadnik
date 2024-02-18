import L from "leaflet";

export const DEFAULT_MAP_LAYER: string = "OpenStreetMap";

export const PathColors: string[] = [
	"maroon",
	"purple",
	"green",
	"olive",
	"navy",
	"teal",
	"red",
	"fuchsia",
	"lime",
	"yellow",
	"blue",
	"aqua",
];

export function GetMapLayers(): L.Control.LayersObject {
	// OpenStreetMap
	var osmUrl = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
		osmAttribution = 'Map Data from <a href="http://www.openstreetmap.org/" target="_blank">OpenStreetMap</a> (<a href="http://creativecommons.org/licenses/by-sa/2.0/" target="_blank">CC-by-SA 2.0</a>)',
		osm = new L.TileLayer(osmUrl, { maxZoom: 18, attribution: osmAttribution });
	// OpenCycleMap
	var cyclemapUrl = '../thunderforest?type=cycle&x={x}&y={y}&z={z}',
		thunderforestAttribution = 'Maps © <a href="https://www.thunderforest.com/" target="_blank">Thunderforest</a>, Data © <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap contributors</a>',
		cyclemap = new L.TileLayer(cyclemapUrl, { maxZoom: 18, attribution: thunderforestAttribution });
	// Landscape Map
	var landscapeMapUrl = '../thunderforest?type=landscape&x={x}&y={y}&z={z}',
		landscapeMap = new L.TileLayer(landscapeMapUrl, { maxZoom: 18, attribution: thunderforestAttribution });
	// Outdoors Map
	var outdoorsMapUrl = '../thunderforest?type=outdoors&x={x}&y={y}&z={z}',
		outdoorsMap = new L.TileLayer(outdoorsMapUrl, { maxZoom: 18, attribution: thunderforestAttribution });
	// Googly Hybrid
	var googleUrl = "https://mts.google.com/vt/lyrs=y&x={x}&y={y}&z={z}",
		googleAttribution = "© 2023 Google",
		google = new L.TileLayer(googleUrl, { maxZoom: 28, attribution: googleAttribution });

	const result = {
		[DEFAULT_MAP_LAYER]: osm,
		"OpenCycleMap": cyclemap,
		"Landscape": landscapeMap,
		"Outdoors": outdoorsMap,
		"Google Hybrid": google
	};

	return result;
}

export function GetMapOverlayLayers() {
	// Waymarked
	var waymarkedshadinghikeUrl = 'http://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png',
		waymarkedshadinghikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
		waymarkedshadinghike = new L.TileLayer(waymarkedshadinghikeUrl, { maxZoom: 18, attribution: waymarkedshadinghikeAttribution });
	// Waymarked
	var waymarkedshadingbikeUrl = 'http://tile.waymarkedtrails.org/cycling/{z}/{x}/{y}.png',
		waymarkedshadingbikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
		waymarkedshadingbike = new L.TileLayer(waymarkedshadingbikeUrl, { maxZoom: 18, attribution: waymarkedshadingbikeAttribution });

	var overlayMaps = {
		"Trails": waymarkedshadinghike,
		"Radrouten": waymarkedshadingbike,
	};

	return overlayMaps;
}

export function GeneratePulsatingCircleIcon(_radius: number, _color: string) : L.DivIcon {
	const cssStyle = `
		width: ${_radius}px;
		height: ${_radius}px;
		background: ${_color};
		color: ${_color};
		box-shadow: 0 0 0 ${_color};
	`;

	const icon = L.divIcon({
		html: `<span style="${cssStyle}" class="circle-marker-pulse"/>`,
		// empty class name to prevent the default leaflet-div-icon to apply
		className: '',
		iconAnchor: [_radius/2, _radius/2]
	});

	return icon;
}

export function GenerateCircleIcon(_radius: number, _color: string) : L.DivIcon {
	const cssStyle = `
		width: ${_radius}px;
		height: ${_radius}px;
		background: ${_color};
		color: ${_color};
		box-shadow: 0 0 0 ${_color};
	`;

	const icon = L.divIcon({
		html: `<span style="${cssStyle}" class="circle-marker"/>`,
		// empty class name to prevent the default leaflet-div-icon to apply
		className: '',
		iconAnchor: [_radius/2, _radius/2]
	});

	return icon;
}
