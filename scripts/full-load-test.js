import http from 'k6/http';
import { check, sleep } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export let options = {
    stages: [
        { duration: '5s', target: 10 },
        { duration: '15s', target: 40 },
        { duration: '5s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<200'], // %95'i 200ms altında olmalı
        http_req_failed: ['rate<0.01'],    // Hata oranı %1'den az olmalı
    },
};

export default function () {
    const url = 'http://localhost:5000/api/users/register';
    const requestId = uuidv4();
    const email = `test_${Math.random().toString(36).substring(7)}@perf.com`;

    const payload = JSON.stringify({
        requestId: requestId,
        email: email,
        password: 'Password123!',
        firstName: 'High',
        lastName: 'Performance'
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    // 1. Orijinal Kayıt
    let res = http.post(url, payload, params);
    if (res.status !== 201) {
        console.log(`Register fail status: ${res.status}, body: ${res.body}`);
    }
    check(res, {
        'register success (201)': (r) => r.status === 201,
    });

    // 2. Idempotency Kontrolü (Aynı RequestId ile tekrar gönder)
    let res2 = http.post(url, payload, params);
    if (res2.status < 200 || res2.status >= 300) {
        console.log(`Idempotency fail status: ${res2.status}, body: ${res2.body}`);
    }
    check(res2, {
        'idempotency success (2xx)': (r) => r.status >= 200 && r.status < 300,
    });

    sleep(0.1);
}
