const axios = require("axios");

const weCall = (conf) => {
  conf.validateStatus = (status) => status > 0 && status < 500;
  return axios(conf);
};

module.exports = {
  weCall,
};
