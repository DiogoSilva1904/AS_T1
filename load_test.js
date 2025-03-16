//comando para correr: k6 run load_test.js

import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '10s', target: 10 }, // Ramp-up: 10 users over 10 seconds
    { duration: '30s', target: 100 }, // Load: 100 users for 30 seconds
    { duration: '10s', target: 0 },  // Ramp-down: 0 users over 10 seconds
  ],
};

export default function () {
  let url = 'http://localhost:5222/api/catalog/items/99?api-version=1.0';
  let res = http.get(url);

  check(res, {
    'status is 200': (r) => r.status === 200,
  });

  sleep(1); // Simulate real user wait time
}
