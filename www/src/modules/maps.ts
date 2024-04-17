import L, { LatLng } from "leaflet";

export const DEFAULT_MAP_LAYER: string = "OpenStreetMap";

export interface IMapState {
	Lat: number;
	Lng: number;
	Zoom: number;
}

export function GetMapLayers(): L.Control.LayersObject {
	// OpenStreetMap
	var osmUrl = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
		osmAttribution = 'Map Data from <a href="http://www.openstreetmap.org/" target="_blank">OpenStreetMap</a> (<a href="http://creativecommons.org/licenses/by-sa/2.0/" target="_blank">CC-by-SA 2.0</a>)',
		osm = new L.TileLayer(osmUrl, { maxZoom: 18, attribution: osmAttribution });
	// OpenCycleMap
	var cyclemapUrl = '../map-tile?type=opencyclemap&x={x}&y={y}&z={z}',
		thunderforestAttribution = 'Maps © <a href="https://www.thunderforest.com/" target="_blank">Thunderforest</a>, Data © <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap contributors</a>',
		cyclemap = new L.TileLayer(cyclemapUrl, { maxZoom: 18, attribution: thunderforestAttribution });
	// Outdoors Map
	var outdoorsMapUrl = '../map-tile?type=tf-outdoors&x={x}&y={y}&z={z}',
		outdoorsMap = new L.TileLayer(outdoorsMapUrl, { maxZoom: 18, attribution: thunderforestAttribution });
	// CartoDb Dark
	var cartoDbDarkUrl = '../map-tile?type=carto-dark&x={x}&y={y}&z={z}',
		cartoDbAttribution = 'Maps © <a href="https://carto.com/attributions" target="_blank">CARTO</a>, Data © <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap contributors</a>',
		cartoDbDark = new L.TileLayer(cartoDbDarkUrl, { maxZoom: 18, attribution: cartoDbAttribution });
	// Googly Hybrid
	var googleUrl = "https://mts.google.com/vt/lyrs=y&x={x}&y={y}&z={z}",
		googleAttribution = "© Google",
		google = new L.TileLayer(googleUrl, { maxZoom: 28, attribution: googleAttribution });

	const result = {
		[DEFAULT_MAP_LAYER]: osm,
		"OpenCycleMap": cyclemap,
		//"Landscape": landscapeMap,
		"Outdoors": outdoorsMap,
		"Carto Dark": cartoDbDark,
		"Google Hybrid": google,
	};

	return result;
}

export function GetMapOverlayLayers() {
	// Waymarked
	const waymarkedshadinghikeUrl = 'https://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png',
		waymarkedshadinghikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
		waymarkedshadinghike = new L.TileLayer(waymarkedshadinghikeUrl, { maxZoom: 18, attribution: waymarkedshadinghikeAttribution });
	// Waymarked
	const waymarkedshadingbikeUrl = 'https://tile.waymarkedtrails.org/cycling/{z}/{x}/{y}.png',
		waymarkedshadingbikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
		waymarkedshadingbike = new L.TileLayer(waymarkedshadingbikeUrl, { maxZoom: 18, attribution: waymarkedshadingbikeAttribution });

	// Strava Heatmap Ride
	const stravaRideUrl = '../map-tile?type=strava-heatmap-ride&x={x}&y={y}&z={z}',
		stravaRideAttribution = '<a href="https://www.strava.com/maps/global-heatmap" target="_blank">Strava Global Heatmap</a>',
		stravaRideLayer = new L.TileLayer(stravaRideUrl, { maxZoom: 18, maxNativeZoom: 16, attribution: stravaRideAttribution });

	// Strava Heatmap Run
	const stravaRunUrl = '../map-tile?type=strava-heatmap-run&x={x}&y={y}&z={z}',
		stravaRunAttribution = '<a href="https://www.strava.com/maps/global-heatmap" target="_blank">Strava Global Heatmap</a>',
		stravaRunLayer = new L.TileLayer(stravaRunUrl, { maxZoom: 18, maxNativeZoom: 16, attribution: stravaRunAttribution, });

	const overlayMaps = {
		"Trails": waymarkedshadinghike,
		"Radrouten": waymarkedshadingbike,
		"Strava Heatmap (Ride)": stravaRideLayer,
		"Strava Heatmap (Run)": stravaRunLayer,
	};

	return overlayMaps;
}

export function GeneratePulsatingCircleIcon(_radius: number, _color: string): L.DivIcon {
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
		iconAnchor: [_radius / 2, _radius / 2]
	});

	return icon;
}

export function GenerateCircleIcon(_radius: number, _color: string): L.DivIcon {
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
		iconAnchor: [_radius / 2, _radius / 2]
	});

	return icon;
}

export function GetMapStateFromCookie(_cookie: string | undefined): IMapState | null {
	if (_cookie === undefined)
		return null;

	const regex = /^([\d\.]+)\:([\d\.]+)\:([\d\.]+)$/g;
	const match = regex.exec(_cookie);
	if (match !== null) {
		const latString = match[1];
		const lat = parseFloat(latString);

		const lngString = match[2];
		const lng = parseFloat(lngString);

		const zoomString = match[3];
		const zoom = parseFloat(zoomString);

		const result: IMapState = {
			Lat: lat,
			Lng: lng,
			Zoom: zoom
		};

		return result;
	}

	return null;
}