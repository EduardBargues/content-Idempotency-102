import http from "k6/http";
import { check } from "k6";
import { Trend, Rate } from "k6/metrics";
import { randomString } from "https://jslib.k6.io/k6-utils/1.1.0/index.js";
import exec from "k6/execution";
const X_IDEM_MARKER = "X-Idem-Marker";
const X_IDEM_DIAGNOSTICS_GET_OWNERSHIP = "X-Idem-Diagnostics-Get-Ownership";
const X_IDEM_DIAGNOSTICS_CACHE_RESPONSE = "X-Idem-Diagnostics-Cache-Response";
const RESPONSE_FROM_CACHE = "response-from-cache";
const RESPONSE_FROM_IMPLEMENTATION = "response-from-implementation";
const api = JSON.parse(open("app.json"));
const url = api.endpoints.value._dotnet_function;

// FUNCTIONS
const checks = {
  statusCode201: (r) => r.status === 201,
  bodyContainsTransactionId: (r) => r.body.transactionId !== "",
  responseFromCache: (r) => r.headers[X_IDEM_MARKER] === RESPONSE_FROM_CACHE,
  responseFromImplementation: (r) =>
    r.headers[X_IDEM_MARKER] === RESPONSE_FROM_IMPLEMENTATION,
};

// CONFIGURATION
export let options = {
  stages: [
    { duration: "10s", target: 5 },
    { duration: "10s", target: 5 },
    { duration: "10s", target: 0 },
  ],
};
const ops = {
  _req0: {
    checks: {
      req0_status_code_201: checks.statusCode201,
      req0_transactionId_informed: checks.bodyContainsTransactionId,
      req0_response_from_implementation: checks.responseFromImplementation,
    },
  },
  _req1: {
    checks: {
      req1_status_code_201: checks.statusCode201,
      req1_transactionId_informed: checks.bodyContainsTransactionId,
      req1_response_from_cache: checks.responseFromCache,
    },
  },
};

// TESTS
const getOwnership = "_idem-get-ownership-time";
const caching = "_idem-caching-time";
let errorRatesByOperationCheck = {};
let trendByOperation = {};
for (const opName in ops) {
  trendByOperation[opName] = new Trend(opName);
  const op = ops[opName];
  for (const checkName in op.checks) {
    errorRatesByOperationCheck[checkName] = new Rate(checkName);
  }
}
trendByOperation[getOwnership] = new Trend(getOwnership);
trendByOperation[caching] = new Trend(caching);

const body = JSON.stringify({
  amount: 1000,
  originId: `origin-id`,
  destinationId: `destination-id`,
});
let headers = {
  "content-type": "application/json",
  "x-idem-for-testing-delay": 1,
  "x-idem-for-testing-success": true,
};

// ITERATION
export default function () {
  const vuId = exec.vu.iterationInInstance;
  const randomText = randomString(4);
  const idempotencyKey = `perf-testing-cachingScenario-${vuId}-${randomText}`;
  headers["x-idem-key"] = idempotencyKey;

  for (const opName in ops) {
    const response = http.post(url, body, { headers: headers });
    // request stats
    trendByOperation[opName].add(response.timings.duration);

    // response checks
    const op = ops[opName];
    for (const checkName in op.checks) {
      let checkInfo = {};
      checkInfo[checkName] = op.checks[checkName];
      check(response, checkInfo) ||
        errorRatesByOperationCheck[checkName].add(1);
    }

    // get ownership stats
    const ownTime = Number(response.headers[X_IDEM_DIAGNOSTICS_GET_OWNERSHIP]);
    trendByOperation[getOwnership].add(ownTime);

    // cache response stats
    if (X_IDEM_DIAGNOSTICS_CACHE_RESPONSE in response.headers) {
      const cachingTime = Number(
        response.headers[X_IDEM_DIAGNOSTICS_CACHE_RESPONSE]
      );
      trendByOperation[caching].add(cachingTime);
    }
  }
}
