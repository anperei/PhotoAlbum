---
name: infrastructure-terraform-generation
description: Generate Terraform IaC files for Azure infrastructure provisioning
---

# Infrastructure Terraform Generation

## Overview

Generate Terraform files to provision Azure infrastructure. Only use when user explicitly requests Terraform.

## Workflow

1. **Gather rules** (call these tools before generating):
   - Call `appmod-get-available-region-sku` to get available regions and SKUs for all needed Azure resource types.
   - Call `appmod-get-iac-rules` with **deploymentTool=azcli** (Do NOT use azd)
   - For Azure landing zone, also call `appmod-get-waf-rules` to ensure Azure Well-Architected Framework compliance
   - Validate all generated files to ensure they are runnable and free of syntax errors: call 'get_errors' on all generated files and iterate until all errors are resolved.

2. **Generate files** Create a subfolder ${taskid} under ${modernization-work-folder} and a subfolder `./infra/`. Generate files in the following structure:

```
/${modernization-work-folder}/${taskid}/
├── plan.md                 # What resources to provision and execution steps

./infra/
├── main.tf                 # Main configuration
├── variables.tf            # Input variables
├── outputs.tf              # Output values
├── providers.tf            # Provider configuration (azurerm)
├── terraform.tfvars        # Variable values
├── modules/                # Reusable Terraform modules
│   └── [resource]/         # One module per resource type
├── deploy.sh               # Deployment script for Linux/macOS
├── deploy.ps1              # Deployment script for Windows
├── README.md               # Infrastructure documentation
├── infra-config.md            # Machine-readable summary of provisioned Azure resources (see template below)
└── compliance.md           # Rules compliance report
```

3. **Deployment scripts** must use Terraform CLI (`terraform init/plan/apply`).

4. **Generate infra-config.md** after successful provisioning following the template in [infra-config-template.md]. This file is critical for downstream tasks (deployment, integration tests) to discover provisioned resources.

## Success Criteria

- All Terraform files pass `terraform validate`
- Deployment scripts are executable
- README.md documents all resources and variables
- plan.md includes clear execution steps for provisioning
- infra-config.md is generated with actual provisioned resource information following the template format, with exactly two required sections: environment details and resource list.
