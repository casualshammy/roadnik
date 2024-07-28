<template>
  <div id="map" class="mapContainer" v-once></div>
</template>

<script setup lang="ts">
import 'leaflet/dist/leaflet.css';
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import L from 'leaflet';
import { type LatLngZoom } from '../data/LatLngZoom';

const emit = defineEmits<{
  (e: 'created', value: L.Map): void
}>();

const props = defineProps<{
  location: LatLngZoom | undefined,
  layers?: L.Layer[] | undefined,
}>();

const map = ref<L.Map>();
const location = computed(() => props.location);

onMounted(() => {
  nextTick(() => {
    initLeafletMap();
  })
})

const initLeafletMap = () => {
  map.value = L.map('map', {
    center: new L.LatLng(props.location?.lat ?? 51.505, props.location?.lng ?? 0),
    zoom: props.location?.zoom ?? 13,
    layers: props?.layers,
    zoomControl: false
  });

  map.value.invalidateSize();

  emit('created', map.value);
}

watch(location, _location => {
  if (_location === undefined || map.value === undefined)
    return;

  if (_location.lat !== undefined && _location.lng !== undefined) {
    map.value.flyTo([_location.lat, _location.lng], _location.zoom, { animate: true, duration: 1 });
  }
  else if (_location.zoom !== undefined) {
    map.value.flyTo(map.value.getCenter(), _location.zoom, { animate: true, duration: 1 });
  }
});

</script>

<style scoped>
.mapContainer {
  width: 100vw;
  height: 100vh;
}
</style>