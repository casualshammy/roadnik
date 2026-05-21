<template>
  <div class="usb-root">
    <!-- Backdrop -->
    <Teleport to="body">
      <div v-if="p_popoverOpen" class="usb-backdrop" @click="closePopover" />
    </Teleport>

    <!-- Popover -->
    <Transition name="usb-pop">
      <div v-if="p_popoverOpen" class="usb-popover">
        <div class="usb-pop-header">
          <input
            ref="p_searchInputEl"
            class="usb-pop-search"
            v-model="p_searchQuery"
            placeholder="Search..."
            @keydown.esc="closePopover" />
          <span class="usb-pop-count">{{ props.appIds.size }} members</span>
        </div>
        <div class="usb-pop-list">
          <template v-if="onlineMembers.length > 0">
            <div class="usb-pop-group">🟢 Online · {{ onlineMembers.length }}</div>
            <div
              v-for="m in onlineMembers" :key="m.appId"
              class="usb-pop-item"
              :class="{ 'usb-pop-item--active': m.appId === props.selectedAppId }"
              @click="onSelectMember(m.appId)">
              <div class="usb-pop-dot" :style="{ background: m.color }" />
              <span class="usb-pop-item-name">{{ m.userName }}</span>
              <div class="usb-pop-item-meta">
                <span class="usb-pop-speed">🚀 {{ m.speedStr }} km/h</span>
                <span class="usb-pop-time">{{ m.timeStr }}</span>
              </div>
            </div>
          </template>
          <template v-if="offlineMembers.length > 0">
            <div class="usb-pop-group">⏸ Offline · {{ offlineMembers.length }}</div>
            <div
              v-for="m in offlineMembers" :key="m.appId"
              class="usb-pop-item"
              :class="{ 'usb-pop-item--active': m.appId === props.selectedAppId }"
              @click="onSelectMember(m.appId)">
              <div class="usb-pop-dot" :style="{ background: m.color }" />
              <span class="usb-pop-item-name">{{ m.userName }}</span>
              <div class="usb-pop-item-meta">
                <span class="usb-pop-speed">🚀 {{ m.speedStr }} km/h</span>
                <span class="usb-pop-time">{{ m.timeStr }}</span>
              </div>
            </div>
          </template>
          <div v-if="onlineMembers.length === 0 && offlineMembers.length === 0" class="usb-pop-empty">
            No members found
          </div>
        </div>
      </div>
    </Transition>

    <!-- Pill: no user selected -->
    <div
      v-if="props.selectedAppId === null"
      class="usb-pill"
      title="Select a user to track"
      @click="togglePopover">
      <span class="usb-pill-icon">👥</span>
      <span class="usb-badge">{{ props.appIds.size }}</span>
    </div>

    <!-- Status bar: user selected -->
    <div v-else-if="selectedUser !== undefined" class="usb-bar">
      <div class="usb-bar-accent" :style="{ background: selectedUser.color }" />
      <div class="usb-bar-body">
        <div class="usb-row1">
          <button
            class="usb-name-link"
            :style="{ color: selectedUser.color, '--usb-user-color': selectedUser.color }"
            :title="`Center map on ${selectedUser.userName}`"
            @click="emit('centerOnUser', props.selectedAppId!)">
            {{ selectedUser.userName }}
          </button>
          <span class="usb-time">{{ selectedTimestamp }}</span>
          <button class="usb-close" title="Stop tracking" @click="emit('deselect')">✕</button>
        </div>
        <div class="usb-row2">
          <span>🚀 {{ selectedUser.speedStr }} km/h</span>
          <span v-if="selectedUser.hrStr !== undefined">{{ selectedUser.hrStr }}</span>
        </div>
        <div class="usb-row3">
          <span v-if="selectedUser.battery !== undefined">🔋 {{ selectedUser.battery }}%</span>
          <span v-if="selectedUser.gsmSignal !== undefined">📶 {{ selectedUser.gsmSignal }}%</span>
          <span>⛰ {{ selectedUser.altitude }} m</span>
          <span v-if="selectedUser.accuracy !== undefined">📡 {{ selectedUser.accuracy }} m</span>
        </div>
      </div>
      <div class="usb-bar-count" title="Show all members" @click="togglePopover">
        <span class="usb-bar-count-icon">👥</span>
        <span class="usb-badge">{{ props.appIds.size }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue';
import { TimeSpan } from '@/toolkit/timespan';
import { getHeartRateString } from '@/toolkit/commonToolkit';
import { getCachedColor } from '@/toolkit/mapToolkit';
import type { AppId } from '@/data/Guid';
import type { TimedStorageEntry } from '@/data/backend';

const ONLINE_THRESHOLD_MS = 5 * 60 * 1000;

type MemberEntry = {
  appId: AppId;
  userName: string;
  color: string;
  speedKmh: number;
  speedStr: string;
  timeStr: string;
  isOnline: boolean;
};

type SelectedUserData = {
  userName: string;
  color: string;
  battery: number | undefined;
  gsmSignal: number | undefined;
  speedStr: string;
  altitude: number;
  accuracy: number | undefined;
  hrStr: string | undefined;
  timestamp: number | undefined;
};

const props = defineProps<{
  appIds: Map<AppId, string>;
  gEntries: Map<AppId, TimedStorageEntry[]>;
  selectedAppId: AppId | null;
}>();

const emit = defineEmits<{
  select: [_appId: AppId];
  deselect: [];
  centerOnUser: [_appId: AppId];
}>();

const p_popoverOpen = ref(false);
const p_searchQuery = ref('');
const p_now = ref(Date.now());
const p_searchInputEl = ref<HTMLInputElement>();
let p_tickerId: ReturnType<typeof setInterval> | undefined;

function elapsedStr(_timestampMs: number): string {
  const elapsed = TimeSpan.fromMilliseconds(p_now.value - _timestampMs);
  if (Math.abs(elapsed.totalSeconds) <= 5)
    return 'just now';

  return `${elapsed.toString(false)} ago`;
}

const memberList = computed<MemberEntry[]>(() => {
  const result: MemberEntry[] = [];
  const query = p_searchQuery.value.toLowerCase();

  for (const [appId, userName] of props.appIds) {
    if (query && !userName.toLowerCase().includes(query))
      continue;

    const entries = props.gEntries.get(appId);
    const lastEntry = entries !== undefined && entries.length > 0
      ? entries[entries.length - 1]
      : undefined;

    const speedKmh = (lastEntry?.Speed ?? 0) * 3.6;
    const timestamp = lastEntry?.UnixTimeMs;
    const isOnline = timestamp !== undefined && (p_now.value - timestamp) < ONLINE_THRESHOLD_MS;

    result.push({
      appId,
      userName,
      color: getCachedColor(appId),
      speedKmh,
      speedStr: speedKmh.toFixed(1),
      timeStr: timestamp !== undefined ? elapsedStr(timestamp) : '—',
      isOnline,
    });
  }

  return result;
});

const onlineMembers = computed(() =>
  memberList.value
    .filter(m => m.isOnline)
    .sort((a, b) => a.userName.localeCompare(b.userName))
);

const offlineMembers = computed(() =>
  memberList.value
    .filter(m => !m.isOnline)
    .sort((a, b) => a.userName.localeCompare(b.userName))
);

const selectedUser = computed<SelectedUserData | undefined>(() => {
  const appId = props.selectedAppId;
  if (appId === null)
    return undefined;

  const entries = props.gEntries.get(appId);
  if (entries === undefined || entries.length === 0)
    return undefined;

  const last = entries[entries.length - 1];
  return {
    userName: last.Username,
    color: getCachedColor(appId),
    battery: last.Battery != null ? Math.round(last.Battery * 100) : undefined,
    gsmSignal: last.GsmSignal != null ? Math.round(last.GsmSignal * 100) : undefined,
    speedStr: ((last.Speed ?? 0) * 3.6).toFixed(1),
    altitude: Math.round(last.Altitude),
    accuracy: last.Accuracy != null ? Math.ceil(last.Accuracy) : undefined,
    hrStr: getHeartRateString(last.HR ?? undefined),
    timestamp: last.UnixTimeMs,
  };
});

const selectedTimestamp = computed(() => {
  const ts = selectedUser.value?.timestamp;
  if (ts === undefined)
    return '';

  return elapsedStr(ts);
});

async function togglePopover() {
  p_popoverOpen.value = !p_popoverOpen.value;
  if (!p_popoverOpen.value) {
    p_searchQuery.value = '';
  } else {
    await nextTick();
    p_searchInputEl.value?.focus();
  }
}

function closePopover() {
  p_popoverOpen.value = false;
  p_searchQuery.value = '';
}

function onSelectMember(_appId: AppId) {
  closePopover();
  emit('select', _appId);
}

onMounted(() => {
  p_tickerId = setInterval(() => { p_now.value = Date.now(); }, 1000);
});

onUnmounted(() => {
  clearInterval(p_tickerId);
});
</script>

<style scoped>
/* ── Root ─────────────────────────────────────────────────────── */
.usb-root {
  position: fixed;
  bottom: 18px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 10000;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  pointer-events: none;
}

.usb-root > * {
  pointer-events: auto;
}

/* ── Backdrop ─────────────────────────────────────────────────── */
.usb-backdrop {
  position: fixed;
  inset: 0;
  z-index: 9999;
}

/* ── Pill ─────────────────────────────────────────────────────── */
.usb-pill {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  background: rgba(255, 255, 255, 0.95);
  border: 1px solid #d1d5db;
  border-radius: 999px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  cursor: pointer;
  transition: box-shadow 0.15s;
  white-space: nowrap;
}

.usb-pill:hover {
  box-shadow: 0 3px 12px rgba(0, 0, 0, 0.2);
}

.usb-pill-icon {
  font-size: 16px;
}

/* ── Badge ────────────────────────────────────────────────────── */
.usb-badge {
  background: #3b82f6;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  line-height: 1;
  padding: 2px 6px;
  border-radius: 999px;
  min-width: 18px;
  text-align: center;
}

/* ── Status Bar ───────────────────────────────────────────────── */
.usb-bar {
  display: flex;
  align-items: stretch;
  background: rgba(255, 255, 255, 0.97);
  border: 1px solid #d1d5db;
  border-radius: 10px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.15);
  overflow: hidden;
  min-width: 240px;
  max-width: 380px;
}

.usb-bar-accent {
  width: 5px;
  flex-shrink: 0;
}

.usb-bar-body {
  flex: 1;
  padding: 7px 10px;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.usb-row1,
.usb-row2,
.usb-row3 {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #374151;
  flex-wrap: wrap;
}

.usb-row1 {
  font-size: 13px;
}

/* Name-link button */
.usb-name-link {
  all: unset;
  cursor: pointer;
  font-weight: 600;
  font-size: 13px;
  text-decoration: underline dotted var(--usb-user-color, #3b82f6);
  text-underline-offset: 3px;
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.usb-name-link:hover {
  text-decoration-style: solid;
}

.usb-time {
  font-size: 11px;
  color: #9ca3af;
  white-space: nowrap;
  flex-shrink: 0;
}

.usb-close {
  all: unset;
  cursor: pointer;
  font-size: 13px;
  color: #9ca3af;
  line-height: 1;
  padding: 1px 3px;
  border-radius: 3px;
  flex-shrink: 0;
}

.usb-close:hover {
  color: #374151;
  background: #f3f4f6;
}

/* Count button (right side) */
.usb-bar-count {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 8px 12px;
  border-left: 1px solid #e5e7eb;
  cursor: pointer;
  flex-shrink: 0;
  transition: background 0.1s;
}

.usb-bar-count:hover {
  background: #f9fafb;
}

.usb-bar-count-icon {
  font-size: 16px;
}

/* ── Popover ──────────────────────────────────────────────────── */
.usb-popover {
  background: #fff;
  border: 1px solid #d1d5db;
  border-radius: 10px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.18);
  width: 300px;
  max-height: 340px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  z-index: 10001;
  pointer-events: auto;
}

.usb-pop-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px 8px;
  border-bottom: 1px solid #f3f4f6;
  flex-shrink: 0;
}

.usb-pop-search {
  flex: 1;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 5px 8px;
  font-size: 12px;
  outline: none;
  background: #f9fafb;
  color: #374151;
  min-width: 0;
}

.usb-pop-search:focus {
  border-color: #93c5fd;
  background: #fff;
}

.usb-pop-count {
  font-size: 11px;
  color: #9ca3af;
  white-space: nowrap;
  flex-shrink: 0;
}

.usb-pop-list {
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: #d1d5db transparent;
  flex: 1;
  padding: 4px 0;
}

.usb-pop-group {
  padding: 6px 12px 3px;
  font-size: 11px;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.usb-pop-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  cursor: pointer;
  transition: background 0.1s;
}

.usb-pop-item:hover {
  background: #f9fafb;
}

.usb-pop-item--active {
  background: #eff6ff;
}

.usb-pop-item--active:hover {
  background: #dbeafe;
}

.usb-pop-dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex-shrink: 0;
}

.usb-pop-item-name {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
  color: #111827;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}

.usb-pop-item-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 1px;
  flex-shrink: 0;
}

.usb-pop-speed {
  font-size: 11px;
  color: #374151;
}

.usb-pop-time {
  font-size: 11px;
  color: #9ca3af;
}

.usb-pop-empty {
  padding: 16px;
  text-align: center;
  font-size: 12px;
  color: #9ca3af;
}

/* ── Transition ───────────────────────────────────────────────── */
.usb-pop-enter-active,
.usb-pop-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}

.usb-pop-enter-from,
.usb-pop-leave-to {
  opacity: 0;
  transform: translateY(8px);
}
</style>
