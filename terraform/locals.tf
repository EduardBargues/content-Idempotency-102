locals {
  prefix = "${var.environment}-${var.service_name}-${var.service_group}"
  lambdas = {
    s3_key_dotnet_function = "artifacts/${var.service_name}/${var.service_version}/${var.service_name}-${var.service_version}-${var.function_name}.zip"
  }
  endpoints = {
    _dotnet_function = "dotnet-function"
  }
  logs_retention_in_days = 1
  apigw_name             = "${local.prefix}-apigw"
  tags = {
    "service-name"    = var.service_name
    "service-version" = var.service_version
    "service-group"   = var.service_group
    "environment"     = var.environment
  }
}
