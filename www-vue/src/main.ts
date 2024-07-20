import { createApp } from 'vue'
import App from './MapView.vue'

console.log("MODE:", import.meta.env.MODE);

const app = createApp(App);
app.mount('#app');
