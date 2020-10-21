using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BibliotecaSimulador.LecturaVariablesSimulador;
using BibliotecaSimulador.Pojos;
using BibliotecaSimulador.SimuladorDAO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WsAdministracionVariables.Negocio;

namespace WsAdministracionVariables.Controllers
{
    [ApiController]
    [Route("[controller]/api/")]
    public class VariablesController : ControllerBase
    {
        private BibliotecaSimulador.Logs.Logg _log;
        private readonly IConfiguration _configuration;
        private readonly string[] extensionesPermitidas = { ".XLS", ".XLSX" };
        private string Nombrelog { get; set; }
        public VariablesController(IConfiguration configuration)
        {
            this._configuration = configuration;
            this.Nombrelog = this._configuration.GetValue<string>("NombreLogVariables");
            this._log = new BibliotecaSimulador.Logs.Logg(this.Nombrelog);
        }
        public IActionResult Index()
        {
            return Ok(new { saludo = "Hola Mundo" });
        }
        [Produces("application/json")]
        [HttpPost("extraerVariables/usuario/{id}")]
        public async Task<IActionResult> ExtraerVariables(int id)
        {
            var rutaArchivo = "";
            try
            {
                Stopwatch time = new Stopwatch();
                time.Start();
                this._log.WriteInfo($"Inicia el servicio para extraer las variables");
                List<TipoProductoFamilia> tipoProductoFamilias = null;
                int cantidadArchivos = Request.Form.Files.Count;
                if (cantidadArchivos > 0 && cantidadArchivos == 1)
                {
                    var archivoExcelPeticion = Request.Form.Files.Select(e => e).ToList()[0];
                    string extensionArchivo = Path.GetExtension(archivoExcelPeticion.FileName)
                                                  .ToUpperInvariant();
                    if (string.IsNullOrEmpty(extensionArchivo) || this.extensionesPermitidas.Contains(extensionArchivo))
                    {
                        ArchivoExcel archivoExcel = null;
                        rutaArchivo = $"{Path.GetTempPath()}variables{DateTime.Now.Ticks}.xlsx";
                        using (var stream = new FileStream(rutaArchivo, FileMode.Create))
                        {
                            await archivoExcelPeticion.CopyToAsync(stream);
                            stream.Close();
                            archivoExcel = new ArchivoExcel(rutaArchivo);
                        }
                        archivoExcel.RutaTemportal = rutaArchivo;
                        tipoProductoFamilias = archivoExcel.ExcelUsingEPPlus().ToList();
                        time.Stop();
                        this._log.WriteAndCountService($"Método:{nameof(ExtraerVariables)}-> Se ejecuto correctamente",
                            new Dictionary<string, int>
                            {
                                {
                                    nameof(WsAdministracionVariables),
                                    Convert.ToInt32(time.ElapsedMilliseconds)
                                }
                            });
                        return Ok(
                            new RespuestaOK
                            {
                                respuesta =
                                new
                                {
                                    productosFamilias = tipoProductoFamilias,
                                    esValido = !(tipoProductoFamilias.Any(e => e.fiFamiliaId == 0))
                                }
                            });
                    }
                    else
                    {
                        time.Stop();
                        this._log.WriteAndCountService($"Método:{nameof(ExtraerVariables)}-> No es un archivo el archivo que se cargo",
                            new Dictionary<string, int>
                            {
                                {
                                    nameof(WsAdministracionVariables),
                                    Convert.ToInt32(time.ElapsedMilliseconds)
                                }
                            });
                        return BadRequest(
                            new RespuestaError400
                            {
                                errorInfo = string.Empty,
                                errorMessage = $"No es un archivo de Excel"
                            });
                    }
                }
                else
                {
                    time.Stop();
                    this._log.WriteAndCountService($"Método:{nameof(ExtraerVariables)}-> Se cargo más de un archivo o no se cargo ningún archivo",
                        new Dictionary<string, int>
                        {
                            {
                                nameof(WsAdministracionVariables),
                                Convert.ToInt32(time.ElapsedMilliseconds)
                            }
                        });
                    return BadRequest(
                        new RespuestaError400
                        {
                            errorInfo = string.Empty,
                            errorMessage = cantidadArchivos > 0 ? "Solo se puede subir un archivo" : "No subiste ningún archivo"
                        });
                }
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException)
                {
                    if (this._log is null)
                    {
                        this._log = new BibliotecaSimulador.Logs.Logg(this.Nombrelog);
                        this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new RespuestaError
                            {
                                errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:CrearArchivoExcel")
                            });
                    }
                    this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new RespuestaError
                        {
                            errorMessage = this._configuration.GetValue<string>($"Mensajes:Errores:Generico")
                        });
                }
                else if (ex is ArgumentNullException)
                {
                    if (this._log is null)
                    {
                        this._log = new BibliotecaSimulador.Logs.Logg(this.Nombrelog);
                        this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new RespuestaError
                            {
                                errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:Argumento")
                            });
                    }
                    this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new RespuestaError
                        {
                            errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:Generico")
                        });
                }
                else
                {
                    this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new RespuestaError
                        {
                            errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:Generico")
                        });
                }
            }
            finally
            {
                if (System.IO.File.Exists(rutaArchivo))
                {
                    System.IO.File.Delete(rutaArchivo);
                }
            }
        }
        [HttpPost("guardarVariablesTodo/usuario/{id}")]
        public IActionResult GuardarValoresTodos(int id, IEnumerable<TipoProductoFamilia> tipoProductosFamilias)
        {
            try
            {
                Stopwatch time = new Stopwatch();
                time.Start();
                this._log.WriteInfo($"Inicia el servicio para el guardado de variables");
                
                bool sonValidasVariables = false;
                if (!(tipoProductosFamilias is null))
                {
                    ValidacionVariables validacionVariables = new ValidacionVariables(this._configuration);
                    validacionVariables.construccionObjetoPeticion(tipoProductosFamilias.ToList());
                    ObjetoRespuestaVariables objetoRespuesta = validacionVariables.RealizarPeticionValidacion();
                    if (!(objetoRespuesta is null))
                    {
                        if (!(objetoRespuesta.Valores is null))
                        {
                            sonValidasVariables = validacionVariables.ValidacionValoresVariables(objetoRespuesta);
                            if (sonValidasVariables)
                            {
                                bool validarValores = false;
                                bool validarVariables = false;
                                foreach (TipoProductoFamilia tipoProductoFamilia in tipoProductosFamilias)
                                {
                                    TccrConfiguracionCalculosDao tCCRConfiguracionCalculosDAO = new TccrConfiguracionCalculosDao();
                                    tCCRConfiguracionCalculosDAO.nombreSPAEjecutar = this._configuration.GetSection("spConfiguracion").Value;
                                    TccrConfiguracionCalculos tCCRConfiguracionCalculos = tCCRConfiguracionCalculosDAO.obtenerTCCRConfiguracionCalculos(1,
                                        null, tipoProductoFamilia.fiTipoProductoId
                                        , null, null, null);
                                    if (!(tCCRConfiguracionCalculos is null))
                                    {
                                        if (tCCRConfiguracionCalculos.fiConfiguracionId > 0)
                                        {
                                            TccrFactoresDao tccrFactoresActualizar = new TccrFactoresDao();
                                            tccrFactoresActualizar.nombreSPAEjecutar = this._configuration.GetSection("spFactores").Value;
                                            int actualizado = tccrFactoresActualizar.establecerFactoresEnCeroPorFamilia(0, tCCRConfiguracionCalculos.fiConfiguracionId
                                                , tipoProductoFamilia.fiFamiliaId);
                                            if (actualizado >= 0)
                                            {
                                                foreach (var variables in tipoProductoFamilia.variablesCabeceras)
                                                {
                                                    if (variables.fiVariableId <= 0)
                                                    {
                                                        int fiVariableIdABuscar = 0;
                                                        TccrVariablesDao tCCRVariablesDAOBuscar = new TccrVariablesDao();
                                                        tCCRVariablesDAOBuscar.nombreSPAEjecutar = this._configuration.GetSection("spVariables").Value;
                                                        fiVariableIdABuscar = tCCRVariablesDAOBuscar.insertarTCCRVariablesPersonalizado(1, null, variables.fcDescripcion, null, null);
                                                        if (fiVariableIdABuscar > 0)
                                                        {
                                                            variables.fiVariableId = fiVariableIdABuscar;
                                                        }
                                                        else
                                                        {
                                                            int fiVariableId = 0;
                                                            TccrVariablesDao tCCRVariablesDAO = new TccrVariablesDao();
                                                            tCCRVariablesDAO.nombreSPAEjecutar = this._configuration.GetSection("spVariables").Value;
                                                            fiVariableId = tCCRVariablesDAO.insertarTCCRVariablesPersonalizado(2, null, variables.fcDescripcion, 1, id);
                                                            if (fiVariableId > 0)
                                                            {
                                                                variables.fiVariableId = fiVariableId;
                                                            }
                                                            else
                                                            {
                                                                validarVariables = true;
                                                            }
                                                        }
                                                    }
                                                    if (variables.fiVariableId > 0)
                                                    {
                                                        foreach (var variablesValores in variables.variables)
                                                        {
                                                            var valorValidadoActual = false;
                                                            TccrFactoresDao tCCRFactoresDAO = new TccrFactoresDao();
                                                            tCCRFactoresDAO.nombreSPAEjecutar = this._configuration.GetSection("spFactores").Value;
                                                            if (variablesValores.esPorcentaje)
                                                            {
                                                                variablesValores.valor = ArchivoExcel.PorcentajeDecimal(variablesValores.valor);
                                                            }
                                                            TccrFactores tCCRFactores = new TccrFactores
                                                            {
                                                                fiConfiguracionId = tCCRConfiguracionCalculos.fiConfiguracionId,
                                                                fiVariableId = variables.fiVariableId,
                                                                fiFamiliaId = tipoProductoFamilia.fiFamiliaId,
                                                                fiPlazo = variablesValores.plazo,
                                                                fnValor = variablesValores.valor,
                                                                fdIniVigencia = null,
                                                                fiStatus = 1,
                                                                fiUsuario = id,
                                                            };
                                                            valorValidadoActual = tCCRFactoresDAO.insertarTCCRFactoresPersonalizado(2, tCCRFactores);
                                                            if (!valorValidadoActual)
                                                            {
                                                                if (!validarValores)
                                                                {
                                                                    validarValores = true;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        validarVariables = true;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                time.Stop();
                                                string mensaje = $"Se tuvo un error al actulizar los factores en cero de la familia ${tipoProductoFamilia.fcDescripcionFamilia}";
                                                this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> {mensaje}",
                                                    new Dictionary<string, int>
                                                    {
                                                        {
                                                            nameof(WsAdministracionVariables),
                                                            Convert.ToInt32(time.ElapsedMilliseconds)
                                                        }
                                                    });
                                                return StatusCode(StatusCodes.Status500InternalServerError,
                                                    new RespuestaError
                                                    {
                                                        errorMessage = mensaje
                                                    });
                                            }
                                        }
                                        else
                                        {
                                            time.Stop();
                                            string mensaje = $"No existe una configuración para el tipo producto: " +
                                                    $"{ tipoProductoFamilia.fiTipoProductoId } para la familia:{ tipoProductoFamilia.fcDescripcionFamilia } , favor de validar";
                                            this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> {mensaje}",
                                                new Dictionary<string, int>
                                                {
                                                        {
                                                            nameof(WsAdministracionVariables),
                                                            Convert.ToInt32(time.ElapsedMilliseconds)
                                                        }
                                                });
                                            return NotFound(
                                                new RespuestaError404
                                                {
                                                    errorMessage = mensaje
                                                });
                                        }
                                    }
                                    else
                                    {
                                        time.Stop();
                                        string mensaje = $"No existe una configuración para el tipo producto: " +
                                                $"{ tipoProductoFamilia.fiTipoProductoId } para la familia:{ tipoProductoFamilia.fcDescripcionFamilia } , favor de validar";
                                        this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> {mensaje}",
                                            new Dictionary<string, int>
                                            {
                                                {
                                                    nameof(WsAdministracionVariables),
                                                    Convert.ToInt32(time.ElapsedMilliseconds)
                                                }
                                            });
                                        return NotFound(
                                            new RespuestaError404
                                            {
                                                errorMessage = mensaje
                                            });
                                    }
                                }
                                time.Stop();
                                this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> Se guardo correctamento los valores",
                                    new Dictionary<string, int>
                                    {
                                        {
                                            nameof(WsAdministracionVariables),
                                            Convert.ToInt32(time.ElapsedMilliseconds)
                                        }
                                    });
                                return Ok(
                                    new RespuestaOK
                                    {
                                        respuesta =
                                        new
                                        {
                                            userId = id,
                                            validacion = sonValidasVariables,
                                            valoresOK = validarValores,
                                            variablesOK = validarVariables
                                        }
                                    });
                            }
                            else
                            {
                                time.Stop();
                                this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> Los valores no validaron correctamente",
                                    new Dictionary<string, int>
                                    {
                                        {
                                            nameof(WsAdministracionVariables),
                                            Convert.ToInt32(time.ElapsedMilliseconds)
                                        }
                                    });
                                return Ok(
                                    new RespuestaOK
                                    {
                                        respuesta =
                                        new
                                        {
                                            userId = id,
                                            validacion = sonValidasVariables,
                                            objeto = validacionVariables.tipoProductoFamilia
                                        }
                                    });
                            }
                        }
                        else
                        {
                            time.Stop();
                            this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> Error al validar los maximo y minimos de las variables: servicio maximos y minimos " +
                                    "no se obtuvieron valores",
                                new Dictionary<string, int>
                                {
                                    {
                                        nameof(WsAdministracionVariables),
                                        Convert.ToInt32(time.ElapsedMilliseconds)
                                    }
                                });
                            return NotFound(
                                new RespuestaError404
                                {
                                    errorMessage = $"Error al validar los maximo y minimos de las variables: servicio maximos y minimos " +
                                    "no se obtuvieron valores"
                                });
                        }
                    }
                    else
                    {
                        time.Stop();
                        this._log.WriteAndCountService($"Método:{nameof(GuardarValoresTodos)}-> Error al validar los maximo y minimos de las variables:  servicio maximos y minimos no disponible",
                            new Dictionary<string, int>
                            {
                                {
                                    nameof(WsAdministracionVariables),
                                    Convert.ToInt32(time.ElapsedMilliseconds)
                                }
                            });
                        return NotFound(
                            new RespuestaError404
                            {
                                errorMessage = $"Error al validar los maximo y minimos de las variables:  servicio maximos y minimos no disponible"
                            });
                    }
                }
                else
                {
                    time.Stop();
                    this._log.WriteAndCountService($"La lista de familias de la peticion viene nula",
                        new Dictionary<string, int>
                        {
                            {
                                nameof(WsAdministracionVariables),
                                Convert.ToInt32(time.ElapsedMilliseconds)
                            }
                        });
                    return NotFound(new RespuestaError400 { });
                }
            }
            catch (Exception ex)
            {
                if (ex is SqlException)
                {
                    if (this._log is null)
                    {
                        this._log = new BibliotecaSimulador.Logs.Logg(this.Nombrelog);
                        this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new RespuestaError
                            {
                                errorMessage = $"Ocurrio un error en la base de datos"
                            });
                    }
                    this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new RespuestaError
                        {
                            errorMessage = $"Ocurrio un error en la base de datos"
                        });
                }
                else if (ex is ArgumentNullException)
                {
                    if (this._log is null)
                    {
                        this._log = new BibliotecaSimulador.Logs.Logg(this.Nombrelog);
                        this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new RespuestaError
                            {
                                errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:Argumento")
                            });
                    }
                    this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new RespuestaError
                        {
                            errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:Argumento")
                        });
                }
                else
                {
                    this._log.WriteErrorService(ex, nameof(WsAdministracionVariables));
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new RespuestaError
                        {
                            errorMessage = this._configuration.GetValue<string>("Mensajes:Errores:Generico")
                        });
                }
            }
        }

    }
}
