using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Features.Products.Commands;
using SalesSaaS.Features.Products.Queries;
using SalesSaaS.Domain;

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
    [Authorize(Roles = Roles.Sales)]
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

        try
        {
            var query = new GetProductsQuery(tenantId, searchTerm, isActive, pageNumber, pageSize);
            var products = await _mediator.Send(query);
            return Ok(products);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // 🚀 GET: api/products/GUID_DEL_PRODUCTO?tenantId=GUID_DEL_TENANT
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio para validar la seguridad.");
        }

        try
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id, tenantId));

            if (product == null)
            {
                return NotFound("El producto solicitado no existe o no pertenece a este Tenant.");
            }

            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // 🚀 POST: api/products
    [HttpPost]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        if (command == null)
        {
            return BadRequest("El cuerpo de la petición no puede ser nulo.");
        }

        try
        {
            var productId = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = productId, tenantId = command.TenantId }, new { id = productId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // 🚀 PUT: api/products/GUID_DEL_PRODUCTO
    [HttpPut("{productId}")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Update(Guid productId, [FromBody] UpdateProductCommand command)
    {
        if (productId != command.Id)
        {
            return BadRequest("El ID del producto no coincide.");
        }

        try
        {
            var updated = await _mediator.Send(command);

            if (!updated)
            {
                return NotFound("El producto no existe o no pertenece a este Tenant.");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // 🚀 DELETE: api/products/GUID_DEL_PRODUCTO?tenantId=GUID_DEL_TENANT
    [HttpDelete("{productId}")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Delete(Guid productId, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio.");
        }

        try
        {
            var command = new DeleteProductCommand(productId, tenantId);
            var deleted = await _mediator.Send(command);

            if (!deleted)
            {
                return NotFound("El producto no existe o no pertenece a este Tenant.");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }
}
