namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Lo que respondió la pasarela al iniciar el cobro de una orden, guardado para devolverlo igual a un reintento del mismo
/// formulario (pendings/589, regla 66).
/// </summary>
/// <remarks>
/// El checkout manda el mismo id de orden en cada envío del mismo intento. Si la respuesta del primero no llegó (un corte de
/// red), el segundo encuentra la orden ya guardada: no se cobra otra vez, y la pantalla tiene que poder seguir esperando el
/// desenlace de ese cobro, o abrir la pasarela, con lo mismo que le habría dicho el primero.
/// </remarks>
/// <param name="Success">Si la pasarela aceptó iniciar el cobro.</param>
/// <param name="NextAction">Lo que tiene que hacer la pantalla después, tal como lo devolvió ms-payments.</param>
/// <param name="RedirectUrl">La dirección de la pasarela, si hay que abrirla.</param>
/// <param name="Metadata">Los datos adicionales que devolvió la pasarela.</param>
public record InitiatedPayment(bool Success, int NextAction, string? RedirectUrl, Dictionary<string, string> Metadata);
