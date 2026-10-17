terraform {
  required_version = ">= 1.11"

  backend "s3" {
    bucket       = "gymnote-tfstate-084250374652"
    key          = "persistent/terraform.tfstate"
    region       = "ap-southeast-1"
    use_lockfile = true
  }
}
