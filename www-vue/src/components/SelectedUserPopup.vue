<template>
  <div
       ref="containerRef"
       @mousedown="onContainerMouseDown"
       @touchstart.passive="onTouchStart"
       :style="{
        position: 'fixed',
        zIndex: '10000',
        border: '5px solid #73AD21',
        color: 'black',
        borderRadius: '5px',
        padding: '10px',
        cursor: 'grab',
        userSelect: 'none',
        touchAction: 'none',
        fontWeight: 'bold',

        left: (props.left ?? 10) + 'px',
        bottom: (props.bottom ?? 10) + 'px',
        borderColor: props.state?.color,
        backgroundColor: bgColor
      }">
    <a
       id="close-btn" href="#"
       title="Close"
       :onclick="onCloseButton"
       :style="{
        background: props.state?.color
      }">✘</a>
    <span class="upper-text">
      <b>{{ props.state?.user }}</b> ({{ timestamp }})
    </span>
    <span class="upper-text">
      🔋{{ battery }}% 📶{{ gsmSignal }}% {{ heartRate }}
    </span>
    <p class="lower-text">
      🚀{{ speed }} km/h ⛰{{ altitude }} m 📡{{ accuracy }} m
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, type Ref } from 'vue';
import * as CommonToolkit from '../toolkit/commonToolkit';
import { TimeSpan } from '@/toolkit/timespan';

export type SelectedUserPopupState = {
  user: string,
  timestamp: number,
  battery?: number | undefined,
  gsmSignal?: number | undefined,
  speed: number,
  altitude: number,
  accuracy?: number | undefined,
  color: string,
  hr?: number
}

const props = defineProps<{
  state?: SelectedUserPopupState | undefined,
  left: number | null,
  bottom: number | null,
}>();

const emit = defineEmits<{
  (e: 'onCloseButton'): void,
  (e: 'onMoved', left: number, bottom: number): void,
  (e: 'onDblClick'): void,
  (e: 'onDraggingStarted'): void,
  (e: 'onDraggingStopped'): void,
}>();

const containerRef: Ref<HTMLDivElement | undefined> = ref();
const isDragging = ref(false);
let updateTimerId: number | undefined = undefined;

const battery = computed(() => ((props.state?.battery ?? 0) * 100).toFixed(0));
const gsmSignal = computed(() => ((props.state?.gsmSignal ?? 0) * 100).toFixed(0));
const speed = computed(() => props.state?.speed.toFixed(1));
const altitude = computed(() => Math.ceil(props.state?.altitude ?? 0));
const accuracy = computed(() => Math.ceil(props.state?.accuracy ?? 100));
const bgColor = computed(() => {
  if (props.state?.color === undefined)
    return '#bebebe';

  const colorBytes = CommonToolkit.colorNameToRgba(props.state?.color);
  if (colorBytes === null)
    return '#bebebe';

  const bgR = Math.min(128 + colorBytes[0] / 4, 255);
  const bgG = Math.min(128 + colorBytes[1] / 4, 255);
  const bgB = Math.min(128 + colorBytes[2] / 4, 255);
  return `#${CommonToolkit.byteArrayToHexString([bgR, bgG, bgB])}`;
});
const timestamp = ref('');
const heartRate = computed(() => {
  const hr = props.state?.hr;
  if (hr === undefined)
    return undefined;

  if (hr < 100)
    return `💚${hr}`
  if (hr < 135)
    return `💛${hr}`
  if (hr < 170)
    return `🧡${hr}`;

  return `❤️${hr}`;
});

function onCloseButton() {
  emit('onCloseButton');
}

function onContainerMouseDown(_e: MouseEvent) {
  const container = containerRef.value;
  if (container === undefined)
    return;

  var offsetX = _e.clientX - parseInt(window.getComputedStyle(container).left);
  var offsetY = window.innerHeight - parseInt(window.getComputedStyle(container).bottom) - _e.clientY;

  function mouseMoveHandler(e: MouseEvent) {
    const style = window.getComputedStyle(container!);
    const left = e.clientX - offsetX;
    const bottom = window.innerHeight - e.clientY - offsetY;
    if (left < 5 || left > window.innerWidth - parseInt(style.width) - 5)
      return;
    if (bottom < 5 || bottom > window.innerHeight - parseInt(style.height) - 5)
      return;

    container!.style.left = left + 'px';
    container!.style.bottom = bottom + 'px';
    emit('onMoved', left, bottom);
  }

  function reset() {
    window.removeEventListener('mousemove', mouseMoveHandler);
    window.removeEventListener('mouseup', reset);
    isDragging.value = false;
    emit('onDraggingStopped');
  }

  window.addEventListener('mousemove', mouseMoveHandler);
  window.addEventListener('mouseup', reset);

  isDragging.value = true;
  emit("onDraggingStarted");
}

function onTouchStart(_e: TouchEvent) {
  const container = containerRef.value;
  if (container === undefined)
    return;

  var offsetX = _e.touches[0].clientX - parseInt(window.getComputedStyle(container).left);
  var offsetY = window.innerHeight - parseInt(window.getComputedStyle(container).bottom) - _e.touches[0].clientY;

  function mouseMoveHandler(e: TouchEvent) {
    const style = window.getComputedStyle(container!);
    const left = e.touches[0].clientX - offsetX;
    const bottom = window.innerHeight - e.touches[0].clientY - offsetY;
    if (left < 0 || left > window.innerWidth - parseInt(style.width))
      return;
    if (bottom < 0 || bottom > window.innerHeight - parseInt(style.height))
      return;

    container!.style.left = left + 'px';
    container!.style.bottom = bottom + 'px';
    emit('onMoved', left, bottom);
  }

  function reset() {
    window.removeEventListener('touchmove', mouseMoveHandler);
    window.removeEventListener('touchend', reset);
    window.removeEventListener('touchcancel', reset);
    isDragging.value = false;
    emit('onDraggingStopped');
  }

  window.addEventListener('touchmove', mouseMoveHandler);
  window.addEventListener('touchend', reset);
  window.addEventListener('touchcancel', reset);

  isDragging.value = true;
  emit("onDraggingStarted");
}

function onDblClick() {
  emit('onDblClick');
}

function updateTimestamp() {
  if (isDragging.value)
    return;

  if (props.state?.timestamp === undefined) {
    timestamp.value = '';
    return;
  }

  const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - props.state.timestamp);
  if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
    timestamp.value = `${elapsedSinceLastUpdate.toString(false)} ago`
  else
    timestamp.value = "now"
}

onMounted(() => {
  containerRef.value?.addEventListener('dblclick', onDblClick);

  updateTimestamp();
  updateTimerId = setInterval(() => updateTimestamp(), 1000);
});

onUnmounted(() => {
  containerRef.value?.removeEventListener('dblclick', onDblClick);
  clearInterval(updateTimerId);
});

</script>

<style scoped>
.upper-text {
  display: block;
  font-size: small;
  text-align: center;
}

.lower-text {
  font-size: smaller;
  margin-bottom: 0px;
}

#close-btn {
  background: #606061;
  color: #FFFFFF;
  line-height: 25px;
  position: absolute;
  right: -12px;
  text-align: center;
  top: -10px;
  width: 24px;
  text-decoration: none;
  font-weight: bold;
  -webkit-border-radius: 12px;
  -moz-border-radius: 12px;
  border-radius: 12px;
}
</style>