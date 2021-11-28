module "get_dotnet_function" {
  source = "./modules/apigw-integrations/lambda"

  rest_api_name         = aws_api_gateway_rest_api.api.name
  endpoint_relative_url = local.endpoints._dotnet_function
  http_method           = "POST"
  lambda_invoke_arn     = aws_lambda_function.lambda_dotnet_function.invoke_arn
  lambda_function_name  = aws_lambda_function.lambda_dotnet_function.function_name

  depends_on = [
    aws_api_gateway_rest_api.api
  ]
}
