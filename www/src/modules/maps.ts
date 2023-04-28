
import * as L from "leaflet"

let customCtrlsCounter = 0;

export interface MapLayerWithName {
	name: string;
	tileLayer: L.TileLayer;
}

export interface MapLayersData {
	obj: L.Control.LayersObject;
	array: MapLayerWithName[];
}

export class LCheckBox implements L.Control {
	private readonly p_baseObj: L.Control;
	private readonly p_elementId: string;

	constructor(_baseObj: L.Control, _elementId: string) {
		this.p_baseObj = _baseObj;
		this.options = _baseObj.options;
		this.p_elementId = _elementId;
	}

	options: L.ControlOptions;

	getPosition(): L.ControlPosition {
		return this.p_baseObj.getPosition();
	}
	setPosition(position: L.ControlPosition): this {
		this.p_baseObj.setPosition(position);
		return this;
	}
	getContainer(): HTMLElement | undefined {
		return this.p_baseObj.getContainer();
	}
	addTo(map: L.Map): this {
		this.p_baseObj.addTo(map);
		return this;
	}
	remove(): this {
		this.p_baseObj.remove();
		return this;
	}
	onAdd?(map: L.Map): HTMLElement {
		if (this.p_baseObj.onAdd !== undefined)
			return this.p_baseObj.onAdd(map);

		return new HTMLElement();
	}
	onRemove?(map: L.Map): void {
		if (this.p_baseObj.onRemove !== undefined)
			this.p_baseObj.onRemove(map);
	}

	setChecked(_value: boolean): void {
		const element = document.getElementById(this.p_elementId) as HTMLInputElement;
		if (element.checked !== _value)
			element.click();
	}
}

export function GetMapLayers(): MapLayersData {
	// OpenStreetMap
	var osmUrl = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
		osmAttribution = 'Map Data from <a href="http://www.openstreetmap.org/" target="_blank">OpenStreetMap</a> (<a href="http://creativecommons.org/licenses/by-sa/2.0/" target="_blank">CC-by-SA 2.0</a>)',
		osm = new L.TileLayer(osmUrl, { maxZoom: 18, attribution: osmAttribution });
	// OpenCycleMap
	var cyclemapUrl = 'thunderforest?type=cycle&x={x}&y={y}&z={z}',
		cyclemap = new L.TileLayer(cyclemapUrl, { maxZoom: 18, attribution: undefined });
	// Landscape Map
	var landscapeMapUrl = 'thunderforest?type=landscape&x={x}&y={y}&z={z}',
		landscapeMap = new L.TileLayer(landscapeMapUrl, { maxZoom: 18, attribution: undefined });
	// Outdoors Map
	var outdoorsMapUrl = 'thunderforest?type=outdoors&x={x}&y={y}&z={z}',
		outdoorsMap = new L.TileLayer(outdoorsMapUrl, { maxZoom: 18, attribution: undefined });
	// Googly Hybrid
	var googleUrl = "https://mts.google.com/vt/lyrs=y&x={x}&y={y}&z={z}",
		googleAttribution = "(c)2015 Google - Map data",
		google = new L.TileLayer(googleUrl, { maxZoom: 28, attribution: googleAttribution });

	var resultArray = Array<MapLayerWithName>(5);
	resultArray[0] = { name: "OpenStreetMap", tileLayer: osm };
	resultArray[1] = { name: "OpenCycleMap", tileLayer: cyclemap };
	resultArray[2] = { name: "Landscape", tileLayer: landscapeMap };
	resultArray[3] = { name: "Outdoors", tileLayer: outdoorsMap };
	resultArray[4] = { name: "Google Hybrid", tileLayer: google };

	var resultObject = {
		[resultArray[0].name]: resultArray[0].tileLayer,
		[resultArray[1].name]: resultArray[1].tileLayer,
		[resultArray[2].name]: resultArray[2].tileLayer,
		[resultArray[3].name]: resultArray[3].tileLayer,
		[resultArray[4].name]: resultArray[4].tileLayer,
	};

	return { obj: resultObject, array: resultArray };
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

export function GetCheckBox(_text: string, _position: string, _handler: (_checked: boolean) => void): LCheckBox {
	const id = `leaflet-custom-ctrl-${customCtrlsCounter++}`;
	const checkbox = L.Control.extend({
		onAdd: function (_map: L.Map): HTMLElement {
			var div = L.DomUtil.create('div');
			div.innerHTML = `
				<div class="leaflet-control-layers leaflet-control-layers-expanded">
				<form>
					<input id="${id}" class="leaflet-control-layers-overlays" id="command" type="checkbox">
					${_text}
					</input>
				</form>
				</div>`;
			setTimeout(() => {
				document.getElementById(id)!.addEventListener("change", _ev => {
					const element = document.getElementById(id) as HTMLInputElement;
					if (element !== null)
						_handler(element.checked);
				}, false);
			}, 1000);
			return div;
		},
		onRemove: function (_map: L.Map) {

		}
	});
	return new LCheckBox(new checkbox({ position: 'topleft' }), id);
}
