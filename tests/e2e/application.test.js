const given = require("./given");
const when = require("./when");
const then = require("./then");
const { beforeAll } = require("@jest/globals");
var faker = require("faker");

const idempotencyKeyPrefix = "e2e-testing";
describe(`GIVEN application is up and running`, () => {
  let app;
  beforeAll(() => (app = given.theApplicationIsUpAndRunning()));

  describe("WHEN calling /dotnet-function for the first time", () => {
    let firstResponse;
    let request;
    const idemKey = `${idempotencyKeyPrefix}-case0-${faker.datatype.string(4)}`;
    beforeAll(async () => {
      request = given.idempotentRequest(
        app.endpoint,
        "post",
        idemKey,
        60,
        5,
        true
      );
      firstResponse = await when.weCall(request);
    });

    it(`THEN should return CREATED-201`, () =>
      then.responseIsCreated(firstResponse));
    it(`THEN should return a transactionId`, () =>
      then.responseIncludesTransaction(firstResponse));
    it(`THEN should come from implementation`, () =>
      then.responseComesFromImplementation(firstResponse));
    it(`THEN should return the idempotency key`, () =>
      then.responseIncludesIdempotencyKey(firstResponse, idemKey));

    describe("WHEN calling /dotnet-function for a 2nd time", () => {
      let secondResponse;
      beforeAll(async () => (secondResponse = await when.weCall(request)));

      it(`THEN should return CREATED-201`, () =>
        then.responseIsCreated(secondResponse));
      it(`THEN should return a transactionId`, () =>
        then.responseIncludesTransaction(secondResponse));
      it(`THEN should come from cache`, () =>
        then.responseComesFromCache(secondResponse));
      it(`THEN should return the idempotency key`, () =>
        then.responseIncludesIdempotencyKey(secondResponse, idemKey));
    });
  });
});
