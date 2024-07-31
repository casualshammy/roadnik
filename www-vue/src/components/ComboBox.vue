<template>
  <div class="custom-select">
    <select
            :value="props.value ?? '-- Paths --'"
            @change="onOptionSelected">
      <template v-for="opt of props.options" :key="opt">
        <option>{{ opt }}</option>
      </template>
    </select>
    <div class="select-arrow"></div>
  </div>
</template>

<script setup lang="ts">

const props = defineProps<{
  options: string[];
  value?: string | undefined;
}>();

const emit = defineEmits<{
  changed: [_value: string]
}>();

function onOptionSelected(_e: Event) {
  const value = (_e.target as HTMLSelectElement)?.value;
  emit('changed', value);
}

</script>

<style scoped>
.custom-select {
  position: relative;
}

.custom-select select {
  appearance: none;
  -webkit-appearance: none;
  width: 100%;
  font-size: 14px;
  font-weight: bold;
  padding: 5px 30px 5px 10px;
  background-color: #FFFFFF;
  border: 1px solid #C4D1EB;
  border-radius: 4px;
  color: #000000;
  cursor: pointer;
  outline: none;
  box-shadow: 3px 3px 2px 0px #E2E2E2;
}

.custom-select select:focus {
  background: #F2F2F2;
  border: 1px solid #5A7EC7;
  border-radius: 5px;
}

.custom-select::after {
  content: "";
  position: absolute;
  pointer-events: none;
  top: 50%;
  right: 10px;
  transform: translate(0, -50%);
  width: 12px;
  height: 12px;
  background-color: #000000;
  clip-path: polygon(50% 0%, 100% 40%, 0% 60%, 100% 60%, 50% 100%, 0% 60%, 100% 40%, 0% 40%);
}

option:first-child {
  font-style: italic;
}
</style>