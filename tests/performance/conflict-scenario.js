import http from "k6/http";
import { check, sleep } from "k6";
import { Trend, Rate } from "k6/metrics";
import { randomString } from "https://jslib.k6.io/k6-utils/1.1.0/index.js";
import exec from "k6/execution";
const X_IDEM_MARKER = "X-Idem-Marker";
const RESPONSE_FROM_CACHE = "response-from-cache";
const RESPONSE_FROM_IMPLEMENTATION = "response-from-implementation";
const api = JSON.parse(open("app.json"));
const url = api.endpoints.value._dotnet_function;
const CONCURRENT_REQUESTS = 2;

// FUNCTIONS
const checks = {
  statusCode201: (r) => r.status === 201,
  bodyContainsTransactionId: (r) => r.body.transactionId !== "",
  bodyContainsMessage: (r) => r.body.message !== "",
  responseFromCacheOrImplementation: (r) =>
    r.headers[X_IDEM_MARKER] === RESPONSE_FROM_CACHE ||
    r.headers[X_IDEM_MARKER] === RESPONSE_FROM_IMPLEMENTATION,
  badGateway: (r) => false,
  serviceUnavailable: (r) => false,
  internalServerError: (r) => false,
};

// CONFIGURATION
export let options = {
  stages: [
    { duration: "10s", target: 5 },
    { duration: "10s", target: 5 },
    { duration: "10s", target: 0 },
  ],
};
const results = {
  201: {
    checks: {
      _created_201_body_has_transactionId: checks.bodyContainsTransactionId,
      _created_201_from_cache_or_implementation:
        checks.responseFromCacheOrImplementation,
    },
  },
  409: {
    checks: {
      _conflict_409_proper_message: checks.bodyContainsMessage,
    },
  },
  500: {
    checks: {
      _internal_server_error_500: checks.internalServerError,
    },
  },
  502: {
    checks: {
      _bad_gateway_502: checks.badGateway,
    },
  },
  503: {
    checks: {
      _service_unavailable_503: checks.serviceUnavailable,
    },
  },
};

// TESTS
let errorRatesByResultCheck = {};
let trendByResult = {};
for (const status in results) {
  trendByResult[status] = new Trend(status);
  const op = results[status];
  for (const checkName in op.checks) {
    errorRatesByResultCheck[checkName] = new Rate(checkName);
  }
}
const body = JSON.stringify({
  amount: 1000,
  originId: `origin-id`,
  destinationId: `destination-id`,
});
const method = "post";
let headers = {
  "content-type": "application/json",
  "x-idem-for-testing-delay": 1000,
  "x-idem-for-testing-success": true,
};
export default function () {
  const vuId = exec.vu.iterationInInstance;
  const randomText = randomString(4);
  const idempotencyKey = `perf-testing-cachingScenario-${vuId}-${randomText}`;
  headers["x-idem-key"] = idempotencyKey;
  const request = {
    method,
    url,
    body,
    params: { headers: headers },
  };
  let requests = [];
  for (var i = 0; i < CONCURRENT_REQUESTS; i++) requests.push(request);

  const responses = http.batch(requests);

  for (var i = 0; i < CONCURRENT_REQUESTS; i++) {
    const response = responses[i];
    const status = response.status;

    if (Object.keys(results).includes(status)) console.log(status);

    trendByResult[status].add(response.timings.duration);

    const result = results[status];
    for (const checkName in result.checks) {
      let checkInfo = {};
      checkInfo[checkName] = result.checks[checkName];
      check(response, checkInfo) || errorRatesByResultCheck[checkName].add(1);
    }
  }
}
