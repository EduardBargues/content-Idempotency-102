const appFile = require("./app.json");
var faker = require("faker");

const theApplicationIsUpAndRunning = () => {
  const app = {
    endpoint: appFile.endpoints.value._dotnet_function,
  };
  return app;
};
const idempotentRequest = (
  endpoint,
  method,
  idempotencyKey,
  timeToLiveInSeconds,
  implementationDelayInMilliseconds,
  implementationSucceed
) => ({
  url: endpoint,
  method,
  headers: {
    "content-type": "application/json",
    "x-idem-key": idempotencyKey,
    "x-idem-ttl": timeToLiveInSeconds,
    "x-idem-for-testing-delay": implementationDelayInMilliseconds,
    "x-idem-for-testing-success": implementationSucceed,
  },
  data: {
    amount: faker.datatype.number(1000),
    originId: faker.datatype.string(4),
    destinationId: faker.datatype.string(4),
  },
});

module.exports = { theApplicationIsUpAndRunning, idempotentRequest };
