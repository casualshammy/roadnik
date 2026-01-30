import L from "leaflet";
import css from '@/css/map.module.css';
import { getColorForString } from "./commonToolkit";

const DEFAULT_MAX_ZOOM = 18;
const p_arrowIconsMap = new Map<string, L.DivIcon>();
const p_colorsMap = new Map<string, string>();

export const DEFAULT_MAP_LAYER: string = "OpenStreetMap";
export const TOLERANT_RENDERER = L.canvas({
	tolerance: 10
});

export interface ICookieMapState {
	Lat: number;
	Lng: number;
	Zoom: number;
}

export function GetMapLayers(_apiUrl: string): L.Control.LayersObject {
	// OpenStreetMap
	const osmAttr = 'Map Data from <a href="http://www.openstreetmap.org/" target="_blank">OpenStreetMap</a> (<a href="http://creativecommons.org/licenses/by-sa/2.0/" target="_blank">CC-by-SA 2.0</a>)';
	const osm = new L.TileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: DEFAULT_MAX_ZOOM, attribution: osmAttr, noWrap: true });
	// OpenCycleMap
	const cyclemapUrl = `${_apiUrl}/api/v1/map-tile/opencyclemap/{z}/{x}/{y}.png`,
		thunderforestAttribution = 'Maps © <a href="https://www.thunderforest.com/" target="_blank">Thunderforest</a>, Data © <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap contributors</a>',
		cyclemap = new L.TileLayer(cyclemapUrl, { maxZoom: DEFAULT_MAX_ZOOM, attribution: thunderforestAttribution, noWrap: true });
	// Outdoors Map
	const outdoorsMapUrl = `${_apiUrl}/api/v1/map-tile/tf-outdoors/{z}/{x}/{y}.png`,
		outdoorsMap = new L.TileLayer(outdoorsMapUrl, { maxZoom: DEFAULT_MAX_ZOOM, attribution: thunderforestAttribution, noWrap: true });

	// Thunderstorm Transport
	const transportLayer = new L.TileLayer(
		`${_apiUrl}/api/v1/map-tile/tf-transport/{z}/{x}/{y}.png`,
		{ maxZoom: DEFAULT_MAX_ZOOM, attribution: thunderforestAttribution, noWrap: true });

	// CartoDb Dark
	const cartoDbDarkUrl = `${_apiUrl}/api/v1/map-tile/carto-dark/{z}/{x}/{y}.png`,
		cartoDbAttribution = 'Maps © <a href="https://carto.com/attributions" target="_blank">CARTO</a>, Data © <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap contributors</a>',
		cartoDbDark = new L.TileLayer(cartoDbDarkUrl, { maxZoom: DEFAULT_MAX_ZOOM, attribution: cartoDbAttribution, noWrap: true });
	// Google Hybrid
	const google = new L.TileLayer(
		"https://mts.google.com/vt/lyrs=y&x={x}&y={y}&z={z}",
		{ maxZoom: 28, attribution: "© Google", noWrap: true });

	const result = {
		[DEFAULT_MAP_LAYER]: osm,
		"OpenCycleMap": cyclemap,
		//"Landscape": landscapeMap,
		"Outdoors": outdoorsMap,
		"Transport": transportLayer,
		"Carto Dark": cartoDbDark,
		"Google Hybrid": google,
	};

	return result;
}

export function GetMapOverlayLayers(_apiUrl: string): L.Control.LayersObject {
	// Waymarked
	const waymarkedshadinghikeUrl = 'https://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png',
		waymarkedshadinghikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
		waymarkedshadinghike = new L.TileLayer(waymarkedshadinghikeUrl, { maxZoom: DEFAULT_MAX_ZOOM, attribution: waymarkedshadinghikeAttribution });
	// Waymarked
	const waymarkedshadingbikeUrl = 'https://tile.waymarkedtrails.org/cycling/{z}/{x}/{y}.png',
		waymarkedshadingbikeAttribution = 'Trails Data by <a href="http://www.waymarkedtrails.org" target="_blank">Waymarkedtrails</a>',
		waymarkedshadingbike = new L.TileLayer(waymarkedshadingbikeUrl, { maxZoom: DEFAULT_MAX_ZOOM, attribution: waymarkedshadingbikeAttribution });

	// Strava Heatmap Ride
	const stravaRideUrl = `${_apiUrl}/api/v1/map-tile/strava-heatmap-ride/{z}/{x}/{y}.png`,
		stravaRideAttribution = '<a href="https://www.strava.com/maps/global-heatmap" target="_blank">Strava Global Heatmap</a>',
		stravaRideLayer = new L.TileLayer(stravaRideUrl, { maxZoom: DEFAULT_MAX_ZOOM, maxNativeZoom: 15, attribution: stravaRideAttribution });

	// Strava Heatmap Run
	const stravaRunUrl = `${_apiUrl}/api/v1/map-tile/strava-heatmap-run/{z}/{x}/{y}.png`,
		stravaRunAttribution = '<a href="https://www.strava.com/maps/global-heatmap" target="_blank">Strava Global Heatmap</a>',
		stravaRunLayer = new L.TileLayer(stravaRunUrl, { maxZoom: DEFAULT_MAX_ZOOM, maxNativeZoom: 15, attribution: stravaRunAttribution, });

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
		display: block;
    border-radius: 50%;
    cursor: pointer;
    animation: ${css.pulse} 2s infinite;
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
		display: block;
    border-radius: 50%;
    cursor: pointer;
	`;

	const icon = L.divIcon({
		html: `<span style="${cssStyle}"/>`,
		// empty class name to prevent the default leaflet-div-icon to apply
		className: '',
		iconAnchor: [_radius / 2, _radius / 2]
	});

	return icon;
}

export function GetMapStateFromCookie(_cookie: string | undefined): ICookieMapState | null {
	if (_cookie === undefined)
		return null;

	const regex = /^([\d.]+):([\d.]+):([\d.]+)$/g;
	const match = regex.exec(_cookie);
	if (match !== null) {
		const latString = match[1];
		const lat = parseFloat(latString);

		const lngString = match[2];
		const lng = parseFloat(lngString);

		const zoomString = match[3];
		const zoom = parseFloat(zoomString);

		const result: ICookieMapState = {
			Lat: lat,
			Lng: lng,
			Zoom: zoom
		};

		return result;
	}

	return null;
}

/**
 * Calculates the initial bearing (forward azimuth) between two geographic points.
 * @param lat1 Latitude of the first point in degrees.
 * @param lon1 Longitude of the first point in degrees.
 * @param lat2 Latitude of the second point in degrees.
 * @param lon2 Longitude of the second point in degrees.
 * @returns The initial bearing in degrees from the first point to the second point.
 */
export function initialBearing(lat1: number, lon1: number, lat2: number, lon2: number): number {
	const toRad = (d: number) => (d * Math.PI) / 180;
	const toDeg = (r: number) => (r * 180) / Math.PI;

	const φ1 = toRad(lat1);
	const φ2 = toRad(lat2);
	const Δλ = toRad(lon2 - lon1);

	const y = Math.sin(Δλ) * Math.cos(φ2);
	const x = Math.cos(φ1) * Math.sin(φ2) - Math.sin(φ1) * Math.cos(φ2) * Math.cos(Δλ);

	const θ = Math.atan2(y, x);
	return (toDeg(θ) + 360) % 360; // normalize to [0..360)
}

export function getCachedArrowIcon(_color: string): L.DivIcon {
	const existingIcon = p_arrowIconsMap.get(_color);
	if (existingIcon)
		return existingIcon;

	const icon = new L.DivIcon({
		className: css.heading_marker,
		html: `<span style="font-size: 24px; color: ${_color}">➤</span>`, //➡
		iconAnchor: [13, 18],
	});

	p_arrowIconsMap.set(_color, icon);
	return icon;
}

export function getCachedColor(_id: string): string {
	const existingColor = p_colorsMap.get(_id);
	if (existingColor)
		return existingColor;

	const newColor = getColorForString(_id);
	p_colorsMap.set(_id, newColor);
	return newColor;
}