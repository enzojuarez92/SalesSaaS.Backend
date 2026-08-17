using MediatR;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Features.Products.Commands;
using SalesSaaS.Features.Products.Queries;

namespace SalesSaaS.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // 🚀 GET: api/products?tenantId=GUID&searchTerm=xxx&isActive=true&pageNumber=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio para consultar productos.");
        }

        var query = new GetProductsQuery(tenantId, searchTerm, isActive, pageNumber, pageSize);
        var products = await _mediator.Send(query);
        return Ok(products);
    }

    // 🚀 GET: api/products/GUID_DEL_PRODUCTO?tenantId=GUID_DEL_TENANT
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio para validar la seguridad.");
        }

        var product = await _mediator.Send(new GetProductByIdQuery(id, tenantId));

        if (product == null)
        {
            return NotFound("El producto solicitado no existe o no pertenece a este Tenant.");
        }

        return Ok(product);
    }

    // 🚀 POST: api/products
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        if (command == null)
        {
            return BadRequest("El cuerpo de la petición no puede ser nulo.");
        }

        var productId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = productId, tenantId = command.TenantId }, new { id = productId });
    }

    // 🚀 PUT: api/products/GUID_DEL_PRODUCTO
    [HttpPut("{productId}")]
    public async Task<IActionResult> Update(Guid productId, [FromBody] UpdateProductCommand command)
    {
        if (productId != command.Id)
        {
            return BadRequest("El ID del producto no coincide.");
        }

        var updated = await _mediator.Send(command);

        if (!updated)
        {
            return NotFound("El producto no existe o no pertenece a este Tenant.");
        }

        return NoContent();
    }

    // 🚀 DELETE: api/products/GUID_DEL_PRODUCTO?tenantId=GUID_DEL_TENANT
    [HttpDelete("{productId}")]
    public async Task<IActionResult> Delete(Guid productId, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio.");
        }

        var command = new DeleteProductCommand(productId, tenantId);
        var deleted = await _mediator.Send(command);

        if (!deleted)
        {
            return NotFound("El producto no existe o no pertenece a este Tenant.");
        }

        return NoContent();
    }
}
