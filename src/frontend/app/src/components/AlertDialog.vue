<template>
  <Teleport to="body">
    <div
         class="alert-dialog__backdrop"
         @click.self="onClose"
         @keydown.esc.stop="onClose"
         tabindex="-1">
      <div
           class="alert-dialog__card"
           role="alertdialog"
           aria-modal="true"
           :aria-labelledby="titleId"
           :aria-describedby="bodyId">
        <div class="alert-dialog__icon-col">
          <div class="alert-dialog__icon" :class="`alert-dialog__icon--${icon}`">
            <span aria-hidden="true">✕</span>
          </div>
        </div>
        <div class="alert-dialog__content">
          <h2 :id="titleId" class="alert-dialog__title">{{ title }}</h2>
          <p :id="bodyId" class="alert-dialog__body">{{ body }}</p>
          <div class="alert-dialog__actions">
            <button
                    type="button"
                    class="alert-dialog__ok"
                    @click="onClose">
              {{ okText }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue';

const props = withDefaults(defineProps<{
  title: string;
  body: string;
  okText?: string;
  icon?: 'error' | 'warning' | 'info';
}>(), {
  okText: 'OK',
  icon: 'error',
});

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const titleId = `alert-dialog-title-${Math.random().toString(36).slice(2, 9)}`;
const bodyId = `alert-dialog-body-${Math.random().toString(36).slice(2, 9)}`;

let previousBodyOverflow: string | null = null;

function onClose() {
  emit('close');
}

function onKeyDown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    e.preventDefault();
    onClose();
  }
}

onMounted(() => {
  previousBodyOverflow = document.body.style.overflow;
  document.body.style.overflow = 'hidden';
  window.addEventListener('keydown', onKeyDown);
});

onUnmounted(() => {
  if (previousBodyOverflow !== null)
    document.body.style.overflow = previousBodyOverflow;
  else
    document.body.style.removeProperty('overflow');

  window.removeEventListener('keydown', onKeyDown);
});
</script>

<style scoped>
.alert-dialog__backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  outline: none;
}

.alert-dialog__card {
  position: relative;
  background: #ffffff;
  border-radius: 8px;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.3),
              0 20px 40px rgba(255, 80, 80, 0.2);
  max-width: 90vw;
  width: 480px;
  max-height: 90vh;
  display: flex;
  gap: 16px;
  padding: 20px 20px 20px 20px;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
}

.alert-dialog__icon-col {
  flex: 0 0 auto;
  display: flex;
  align-items: flex-start;
  padding-top: 2px;
}

.alert-dialog__icon {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  font-size: 22px;
  font-weight: bold;
  line-height: 1;
}

.alert-dialog__icon--error {
  background: #ff5555;
}

.alert-dialog__icon--warning {
  background: #f5a623;
}

.alert-dialog__icon--info {
  background: #4a90e2;
}

.alert-dialog__content {
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.alert-dialog__title {
  margin: 0 0 8px 0;
  padding: 0;
  font-size: 16px;
  font-weight: 600;
  color: #333333;
  line-height: 1.3;
}

.alert-dialog__body {
  margin: 0 0 16px 0;
  padding: 0;
  font-size: 14px;
  color: #555555;
  line-height: 1.5;
  white-space: pre-line;
  overflow-y: auto;
}

.alert-dialog__actions {
  display: flex;
  justify-content: flex-end;
}

.alert-dialog__ok {
  background: #606061;
  color: #ffffff;
  border: none;
  border-radius: 4px;
  padding: 8px 24px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s ease;
}

.alert-dialog__ok:hover {
  background: #4a4a4b;
}

.alert-dialog__ok:focus {
  outline: 2px solid #4a90e2;
  outline-offset: 2px;
}
</style>
