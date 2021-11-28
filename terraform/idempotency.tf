module "idempotency" {
  source = "./modules/idempotency"

  prefix             = local.prefix
  partition_key_name = var.dynamo_hash_key_name
  ttl_attribute_name = var.dynamo_ttl_attribute_name
}
