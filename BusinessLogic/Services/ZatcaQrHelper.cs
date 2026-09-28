using System.Text;

namespace BusinessLogic.Services;

public static class ZatcaQrHelper
{
    // ZATCA QR (TLV) field tags:
    // 1 = Seller Name, 2 = VAT Registration Number, 3 = Invoice Timestamp,
    // 4 = Invoice Total (with VAT), 5 = VAT Amount
    private static byte[] EncodeTlv(byte tag, string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value ?? "");
        var length = valueBytes.Length;
        var result = new List<byte> { tag };

        // TLV length encoding: if length > 0xFF, encode as multi-byte (rare case, keep simple)
        if (length > 0xFF)
        {
            var lenBytes = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            int index = 0;
            while (index < 4 && lenBytes[index] == 0) index++;
            int significantCount = 4 - index;
            result.Add((byte)(0x80 | significantCount));
            for (int i = index; i < 4; i++) result.Add(lenBytes[i]);
        }
        else
        {
            result.Add((byte)length);
        }

        result.AddRange(valueBytes);
        return result.ToArray();
    }

    public static string BuildZatcaQrData(
        string sellerName,
        string vatNumber,
        DateTime invoiceDateTime,
        decimal invoiceTotalWhitVat,
        decimal vatAmount)
    {
        var tlv = new List<byte>();
        tlv.AddRange(EncodeTlv(1, sellerName));
        tlv.AddRange(EncodeTlv(2, vatNumber));
        tlv.AddRange(EncodeTlv(3, invoiceDateTime.ToString("yyyy-MM-ddTHH:mm:ss")));
        tlv.AddRange(EncodeTlv(4, invoiceTotalWhitVat.ToString("0.00")));
        tlv.AddRange(EncodeTlv(5, vatAmount.ToString("0.00")));
        return Convert.ToBase64String(tlv.ToArray());
    }

    public static byte[] GeneratePng(string qrData, int pixelsPerModule = 10)
    {
        using (var generator = new QRCoder.QRCodeGenerator())
        {
            var data = generator.CreateQrCode(qrData, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new QRCoder.PngByteQRCode(data))
            {
                return qrCode.GetGraphic(pixelsPerModule);
            }
        }
    }

    public static bool IsValidTrn(string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber)) return false;
        return vatNumber.Length is >= 15 and <= 20 && vatNumber.All(char.IsDigit);
    }

    public static string GenerateQrImageTag(
        string sellerName,
        string vatNumber,
        DateTime invoiceDateTime,
        decimal invoiceTotalWithVat,
        decimal vatAmount,
        int size = 100)
    {
        var qrData = BuildZatcaQrData(sellerName, vatNumber, invoiceDateTime, invoiceTotalWithVat, vatAmount);
        var png = GeneratePng(qrData);
        var base64 = Convert.ToBase64String(png);
        return $"<img style=\"width:{size}px;height:{size}px;\" src=\"data:image/png;base64,{base64}\" alt=\"QR Code\" />";
    }
}