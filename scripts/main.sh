set -e 
work_folder=$(pwd)
appFile="app.json"

echo ""
echo "--> UNIT TESTS"
cd $work_folder/src
    dotnet test
cd $work_folder

echo ""
echo "--> CREATE ARTIFACTS"
version="dev"
service_name="idem-102"
function_name="Lambda.Serverless"
app_framework="netcoreapp3.1"
output_package="$work_folder/$service_name-$version-$function_name.zip"
dotnet lambda package \
    -c Release \
    -f $app_framework \
    --project-location "$work_folder/src/Lambda.Serverless" \
    --output-package $output_package

source $work_folder/scripts/secrets.sh

s3_destination="s3://$bucket_name/artifacts/$service_name/$version/$service_name-$version-$function_name.zip"
aws s3 cp $output_package $s3_destination

echo ""
echo "--> DEPLOYMENT"
cd $work_folder/terraform
    terraform init -backend-config=backend.tfvars
    terraform validate
    terraform apply -var-file=terraform.tfvars -auto-approve
    rm -f $appFile
    terraform output -json >> $appFile
cd $work_folder

echo ""
echo "--> E2E TESTING"
cd $work_folder/tests/e2e
    rm -f $appFile
    cp "$work_folder/terraform/$appFile" $appFile
    npm install
    npm run e2e
cd $work_folder

echo ""
echo "--> PERFORMANCE TESTING"
cd $work_folder/tests/performance
    rm -f $appFile
    cp "$work_folder/terraform/$appFile" $appFile
    k6 run caching-scenario.js
    k6 run conflict-scenario.js
cd $work_folder

echo ""
echo "--> DESTRUCTION"
cd $work_folder/terraform
    terraform destroy -var-file=terraform.tfvars -auto-approve
cd $work_folder