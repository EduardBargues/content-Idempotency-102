variable "prefix" {
  type        = string
  description = "resource's names will be prefixed with it."
}

variable "partition_key_name" {
  type        = string
  description = "name of the field for your idempotency key"
}

variable "ttl_attribute_name" {
  type        = string
  description = "name of the field that idempotency is going to use to know when to deprecate a key item"
}
