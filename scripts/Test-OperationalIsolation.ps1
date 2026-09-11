param([string]$BaseUrl = 'http://127.0.0.1:5289')
$ErrorActionPreference = 'Stop'
if ($BaseUrl -notmatch '^http://(127\.0\.0\.1|localhost):5289$') { throw 'Usar solamente la API de pruebas aislada en el puerto 5289.' }
$passed = 0
function CallApi($method, $path, $body = $null, $headers = @{}, $expected = 200) {
    $options = @{ Uri = "$BaseUrl/api$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true; TimeoutSec = 60 }
    if ($null -ne $body) { $options.Body = ConvertTo-Json $body -Depth 12 -Compress; $options.ContentType = 'application/json' }
    $response = Invoke-WebRequest @options
    if ([int]$response.StatusCode -ne $expected) { throw "$method $path esperaba $expected, recibió $($response.StatusCode): $($response.Content)" }
    $script:passed++
    if ($response.Content -and $response.Headers['Content-Type'] -match 'json') { return ($response.Content | ConvertFrom-Json) }
}
function Check($condition, $message) { if (!$condition) { throw $message }; $script:passed++ }
$suffix = [Guid]::NewGuid().ToString('N')
$password = 'Audit-Only-Strong-2026!'
$a = CallApi POST /auth/register-tenant @{tenantName="Audit A $suffix";taxId='';firstName='Audit';lastName='Tester';email="a-$suffix@example.invalid";password=$password} @{} 201
$b = CallApi POST /auth/register-tenant @{tenantName="Audit B $suffix";taxId='';firstName='Audit';lastName='Tester';email="b-$suffix@example.invalid";password=$password} @{} 201
$ha = @{Authorization="Bearer $($a.accessToken)"}; $hb = @{Authorization="Bearer $($b.accessToken)"}
$wa = @(CallApi GET "/warehouses?tenantId=$($a.tenantId)" $null $ha)[0].id
$wb = @(CallApi GET "/warehouses?tenantId=$($b.tenantId)" $null $hb)[0].id
CallApi GET "/settings/business?tenantId=$($b.tenantId)" $null $ha 403 | Out-Null
CallApi GET "/products?tenantId=$($a.tenantId)" $null $ha 400 | Out-Null
$ha['X-Warehouse-Id']=$wb
CallApi GET "/products?tenantId=$($a.tenantId)" $null $ha 403 | Out-Null
$ha['X-Warehouse-Id']=$wa
$plans = @(CallApi GET /subscription/plans $null $ha)
CallApi POST /billing/plans @{} $ha 403 | Out-Null
$plan = $plans | Where-Object { $_.maxWarehouses -gt 1 } | Select-Object -First 1
Check ($null -ne $plan) 'No hay plan multidepósito para la prueba.'
CallApi POST /subscription/checkout @{tenantId=$a.tenantId;subscriptionPlanId=$plan.id;annualBilling=$false;autoRenew=$false;paymentProvider='MercadoPago'} $ha | Out-Null
CallApi POST /warehouses @{tenantId=$a.tenantId;code='SECOND';name='Segundo depósito';address='Prueba'} $ha 201 | Out-Null
$w2 = @(CallApi GET "/warehouses?tenantId=$($a.tenantId)" $null $ha) | Where-Object { $_.code -eq 'SECOND' } | Select-Object -ExpandProperty id
$category = CallApi POST /categories @{tenantId=$a.tenantId;name='Prueba';description='Prueba'} $ha 201
Check ($null -ne $category.id) 'Alta rápida de categoría sin ID.'
$product = CallApi POST /products @{tenantId=$a.tenantId;sku='TEST-01';name='Producto';description='Prueba';price=100;cost=50;stock=10;minimumStockAlert=1;categoryId=$category.id;initialWarehouseId=$wa} $ha 201
$customers = CallApi GET "/customers?tenantId=$($a.tenantId)" $null $ha
$customer = $customers.items[0]
$order = @{tenantId=$a.tenantId;customerId=$customer.id;warehouseId=$wa;items=@(@{productId=$product.id;quantity=2});paymentMethod=1;requestId=[Guid]::NewGuid().ToString()}
CallApi POST /orders $order $ha 400 | Out-Null
CallApi POST /cash/open @{tenantId=$a.tenantId;warehouseId=$wa;openingBalance=100} $ha 201 | Out-Null
$sale = CallApi POST /orders $order $ha 201
$repeated = CallApi POST /orders $order $ha 201
Check ($repeated.id -eq $sale.id) 'Un reintento duplicó la venta.'
$invoice = CallApi POST /invoices/issue @{tenantId=$a.tenantId;orderId=$sale.id;documentType=7} $ha
$reprint = CallApi POST /invoices/issue @{tenantId=$a.tenantId;orderId=$sale.id;documentType=7} $ha
Check ($invoice.invoiceId -eq $reprint.invoiceId) 'Un reintento duplicó el comprobante.'
$ledger = CallApi GET "/inventory-tools/products/$($product.id)/kardex" $null $ha
Check ($ledger.balance -eq 8) 'Kardex no refleja stock inicial menos venta.'
Check ($ledger.items[0].stockBefore -eq 10 -and $ledger.items[0].stockAfter -eq 8) 'Kardex anterior/resultante incorrecto.'
Check ($ledger.items[0].user -eq 'Audit Tester') 'Kardex no conserva el responsable.'
$template = Join-Path ([IO.Path]::GetTempPath()) "salessaas-template-$suffix.xlsx"
try {
    Invoke-WebRequest "$BaseUrl/api/inventory-tools/products/template" -Headers $ha -OutFile $template
    $imported = Invoke-RestMethod "$BaseUrl/api/inventory-tools/products/import" -Method Post -Headers $ha -Form @{file=Get-Item $template}
    Check ($imported.imported -eq 1) 'La plantilla válida no se importó.'
    $duplicate = Invoke-RestMethod "$BaseUrl/api/inventory-tools/products/import" -Method Post -Headers $ha -Form @{file=Get-Item $template}
    Check ($duplicate.imported -eq 0 -and $duplicate.errors.Count -eq 1) 'La importación duplicada no informa la fila inválida.'
    foreach($kind in @('products','inventory','accounts')) {
        $export = Invoke-WebRequest "$BaseUrl/api/inventory-tools/export/$kind" -Headers $ha
        Check ($export.Headers['Content-Type'] -match 'spreadsheetml') "Exportación $kind no es XLSX."
    }
} finally { if(Test-Path -LiteralPath $template){Remove-Item -LiteralPath $template} }
$creditCustomer = CallApi POST /customers @{tenantId=$a.tenantId;name='Cliente fiado';legalName='Cliente fiado';documentType='DNI';documentNumber='12345678';taxCondition='Consumidor Final';email='';phone='';address='';city='';state='';postalCode='';creditLimit=5000;allowCredit=$true} $ha 201
$creditOrder = CallApi POST /orders @{tenantId=$a.tenantId;customerId=$creditCustomer.id;warehouseId=$wa;items=@(@{productId=$product.id;quantity=2});paymentMethod=6;requestId=[Guid]::NewGuid().ToString()} $ha 201
$statement = CallApi GET "/customers/$($creditCustomer.id)/statement?tenantId=$($a.tenantId)" $null $ha
Check ($statement.currentBalance -eq 200) 'El fiado no impactó en el estado de cuenta.'
CallApi POST /invoices/issue @{tenantId=$a.tenantId;orderId=$creditOrder.id;documentType=7} $ha | Out-Null
$statement = CallApi GET "/customers/$($creditCustomer.id)/statement?tenantId=$($a.tenantId)" $null $ha
Check ($statement.currentBalance -eq 200) 'Emitir el comprobante duplicó la deuda.'
CallApi POST "/customers/$($creditCustomer.id)/payments" @{tenantId=$a.tenantId;customerId=$creditCustomer.id;warehouseId=$wa;amount=100;description='Pago prueba'} $ha 201 | Out-Null
$statement = CallApi GET "/customers/$($creditCustomer.id)/statement?tenantId=$($a.tenantId)" $null $ha
Check ($statement.currentBalance -eq 100) 'El cobro no redujo el saldo de la sucursal.'
$ha['X-Warehouse-Id']=$w2
$catalog = CallApi GET "/products?tenantId=$($a.tenantId)" $null $ha
Check (@($catalog.items | Where-Object {$_.stock -ne 0}).Count -eq 0) 'El catálogo mezcla existencias entre sucursales.'
$sales = @(CallApi GET "/reports/sales-summary?tenantId=$($a.tenantId)" $null $ha)
Check ($sales.Count -eq 0) 'El reporte muestra ventas de otra sucursal.'
CallApi POST /orders $order $ha 403 | Out-Null
CallApi GET "/invoices/$($sale.id)/pdf?tenantId=$($b.tenantId)" $null $ha 403 | Out-Null
CallApi PUT /profile @{firstName='Audit';lastName='Updated';email=$a.email;currentPassword=$password;newPassword='New-Audit-Password-2026!'} $ha | Out-Null
CallApi GET /profile $null $ha 401 | Out-Null
Write-Output "PASS: $passed comprobaciones HTTP y de consistencia. Negocios de prueba: Audit A/B $suffix."
