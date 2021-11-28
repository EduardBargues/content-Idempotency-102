const HTTP_STATUS_CODE_CREATED = 201;
const IDEM_KEY = "x-idem-key";
const IDEM_HEADER_MARKER = "x-idem-marker";
const IDEM_FROM_IMPLEMENTATION = "response-from-implementation";
const IDEM_FROM_CACHE = "response-from-cache";

const responseIsCreated = (response) => {
  expect(response.status).toBe(HTTP_STATUS_CODE_CREATED);
};
const responseIncludesTransaction = (response) => {
  expect(response.data).toBeDefined();
  expect(response.data.transactionId).toBeDefined();
};
const responseIncludesIdempotencyKey = (response, idemKey) => {
  expect(response.headers[IDEM_KEY]).toBe(idemKey);
};
const responseComesFromImplementation = (response) => {
  expect(response.headers[IDEM_HEADER_MARKER]).toBe(IDEM_FROM_IMPLEMENTATION);
};
const responseComesFromCache = (response) => {
  expect(response.headers[IDEM_HEADER_MARKER]).toBe(IDEM_FROM_CACHE);
};
const oneAndOnlyOneResponseIs = (responses, statusCode) => {
  const number = responses.filter(
    (response) => response.status === statusCode
  ).length;
  expect(number).toBe(1);
};
module.exports = {
  responseIsCreated,
  responseIncludesTransaction,
  responseIncludesIdempotencyKey,
  responseComesFromImplementation,
  responseComesFromCache,
  oneAndOnlyOneResponseIs,
};
