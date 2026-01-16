import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { Counter, Rate, Trend } from 'k6/metrics';

// Custom Metrics
const registerDuration = new Trend('register_duration', true);
const loginDuration = new Trend('login_duration', true);
const profileDuration = new Trend('profile_duration', true);
const listDuration = new Trend('list_duration', true);
const successRate = new Rate('success_rate');
const errorCounter = new Counter('errors');

export let options = {
    scenarios: {
        quick_load: {
            executor: 'constant-vus',
            vus: 50,
            duration: '30s',
            exec: 'complexFlow',
        },
        quick_read: {
            executor: 'constant-vus',
            vus: 50,
            duration: '30s',
            exec: 'keysetReadFlow',
            startTime: '30s',
        },
    },
    thresholds: {
        'http_req_duration': ['p(95)<1000'],
        'http_req_failed': ['rate<0.05'],
        'success_rate': ['rate>0.95'],
    },
};

const BASE_URL = 'http://localhost:5238/api/users';

// Thread-local state for keyset pagination
let lastCreatedAt = null;
let lastId = null;

export function complexFlow() {
    const requestId = uuidv4();
    const email = `test_${uuidv4().substring(0, 8)}@perf.com`;
    const password = 'Password123!';

    group('User Lifecycle', function () {
        // 1. Register
        const regPayload = JSON.stringify({
            requestId: requestId,
            email: email,
            password: password,
            firstName: 'Load',
            lastName: 'Tester'
        });

        const startReg = Date.now();
        let res = http.post(`${BASE_URL}/register`, regPayload, {
            headers: { 'Content-Type': 'application/json' },
            tags: { name: 'Register' }
        });
        registerDuration.add(Date.now() - startReg);

        let userId = null;
        const regSuccess = check(res, { 'registered': (r) => r.status === 201 });
        successRate.add(regSuccess);

        if (regSuccess) {
            try { userId = res.json().userId; } catch (e) { /* ignore */ }
        } else {
            errorCounter.add(1);
            console.log(`[REGISTER FAIL] ${res.status}: ${res.body}`);
        }

        if (userId) {
            // 2. Login
            const loginPayload = JSON.stringify({ email: email, password: password });

            const startLogin = Date.now();
            let loginRes = http.post(`${BASE_URL}/login`, loginPayload, {
                headers: { 'Content-Type': 'application/json' },
                tags: { name: 'Login' }
            });
            loginDuration.add(Date.now() - startLogin);

            let token = null;
            const loginSuccess = check(loginRes, { 'logged in': (r) => r.status === 200 });
            successRate.add(loginSuccess);

            if (loginSuccess) {
                try { token = loginRes.json().token; } catch (e) { /* ignore */ }
            } else {
                errorCounter.add(1);
                console.log(`[LOGIN FAIL] ${loginRes.status}: ${loginRes.body}`);
            }

            if (token) {
                const authHeader = { 'Authorization': `Bearer ${token}` };

                // 3. Get Profile
                const startProfile = Date.now();
                let profileRes = http.get(`${BASE_URL}/${userId}`, {
                    headers: authHeader,
                    tags: { name: 'GetProfile' }
                });
                profileDuration.add(Date.now() - startProfile);

                const profileSuccess = check(profileRes, { 'got profile': (r) => r.status === 200 });
                successRate.add(profileSuccess);
                if (!profileSuccess) {
                    errorCounter.add(1);
                    console.log(`[GET PROFILE FAIL] ${profileRes.status}: ${profileRes.body}`);
                }

                // 4. Update Profile
                const updatePayload = JSON.stringify({
                    firstName: 'Updated',
                    lastName: 'User',
                    roles: 1
                });
                let updateRes = http.put(`${BASE_URL}/${userId}`, updatePayload, {
                    headers: Object.assign({}, authHeader, { 'Content-Type': 'application/json' }),
                    tags: { name: 'UpdateProfile' }
                });

                const updateSuccess = check(updateRes, { 'updated profile': (r) => r.status === 204 });
                successRate.add(updateSuccess);
                if (!updateSuccess) {
                    errorCounter.add(1);
                    console.log(`[UPDATE PROFILE FAIL] ${updateRes.status}: ${updateRes.body}`);
                }
            }
        }
    });

    sleep(Math.random() * 0.5 + 0.1); // 100-600ms random think time
}

export function keysetReadFlow() {
    let url = `${BASE_URL}?pageSize=20`;
    if (lastCreatedAt && lastId) {
        url += `&afterCreatedAt=${encodeURIComponent(lastCreatedAt)}&afterId=${lastId}`;
    }

    const startList = Date.now();
    let res = http.get(url, {
        tags: { name: 'ListUsersKeyset' }
    });
    listDuration.add(Date.now() - startList);

    const success = check(res, { 'list success': (r) => r.status === 200 });
    successRate.add(success);

    if (success) {
        try {
            const data = res.json();
            if (data && data.items && data.items.length > 0) {
                const items = data.items;
                const lastItem = items[items.length - 1];
                lastCreatedAt = lastItem.createdAt;
                lastId = lastItem.id;
            } else {
                // Reset pagination when no more items
                lastCreatedAt = null;
                lastId = null;
            }
        } catch (e) {
            lastCreatedAt = null;
            lastId = null;
        }
    } else {
        errorCounter.add(1);
        lastCreatedAt = null;
        lastId = null;
    }

    sleep(0.1); // Minimal think time for read operations
}

export function handleSummary(data) {
    return {
        'stdout': textSummary(data, { indent: '  ', enableColors: true }),
    };
}

function textSummary(data, opts) {
    const dur = data.metrics.http_req_duration;
    const failed = data.metrics.http_req_failed;
    const reqs = data.metrics.http_reqs;

    return `
================================================================================
                        📊 KAPSAMLI PERFORMANS TEST RAPORU
================================================================================

📈 GENEL SONUÇLAR:
   ├── Toplam İstek: ${reqs?.values?.count || 0}
   ├── Başarısız: ${(failed?.values?.rate * 100 || 0).toFixed(2)}%
   └── RPS: ${(reqs?.values?.rate || 0).toFixed(1)}

⏱️  YANIT SÜRELERİ:
   ├── Ortalama: ${(dur?.values?.avg || 0).toFixed(2)}ms
   ├── Medyan (p50): ${(dur?.values?.med || 0).toFixed(2)}ms
   ├── P95: ${(dur?.values?.['p(95)'] || 0).toFixed(2)}ms
   └── P99: ${(dur?.values?.['p(99)'] || 0).toFixed(2)}ms

📊 CUSTOM METRİKLER:
   ├── Register P95: ${(data.metrics.register_duration?.values?.['p(95)'] || 0).toFixed(2)}ms
   ├── Login P95: ${(data.metrics.login_duration?.values?.['p(95)'] || 0).toFixed(2)}ms
   ├── Profile P95: ${(data.metrics.profile_duration?.values?.['p(95)'] || 0).toFixed(2)}ms
   └── List P95: ${(data.metrics.list_duration?.values?.['p(95)'] || 0).toFixed(2)}ms

${Object.entries(data.root_group?.checks || {}).map(([name, check]) =>
        `✅ ${name}: ${((check.passes / (check.passes + check.fails)) * 100).toFixed(1)}%`
    ).join('\n   ')}

================================================================================
`;
}

export default function () { complexFlow(); }
