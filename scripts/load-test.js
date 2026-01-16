import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '30s', target: 50 }, // 30 sn'de 50 kullanıcıya çık
    { duration: '1m', target: 50 },  // 1 dk boyunca 50 kullanıcıyı koru
    { duration: '10s', target: 0 },  // 10 sn'de kapat
  ],
};

export default function () {
  let res = http.get('http://api:8080/health/ready');
  check(res, {
    'status is 200': (r) => r.status === 200,
  });
  sleep(1);
}
