resource "aws_dynamodb_table" "main" {
  name         = "${var.prefix}-idempotency"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = var.partition_key_name

  attribute {
    name = var.partition_key_name
    type = "S"
  }

  ttl {
    enabled        = true
    attribute_name = var.ttl_attribute_name
  }
}

output "table_name" {
  value = aws_dynamodb_table.main.name
}

output "table_arn" {
  value = aws_dynamodb_table.main.arn
}
