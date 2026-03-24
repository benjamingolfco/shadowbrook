import { useRef } from 'react';
import { QRCodeCanvas } from 'qrcode.react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Download, Printer } from 'lucide-react';

interface QrCodePanelProps {
  shortCode: string;
}

export function QrCodePanel({ shortCode }: QrCodePanelProps) {
  const qrRef = useRef<HTMLDivElement>(null);
  const shortUrl = `${window.location.origin}/w/${shortCode}`;

  function handleDownload() {
    const canvas = qrRef.current?.querySelector('canvas');
    if (!canvas) return;

    const url = canvas.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = url;
    a.download = `waitlist-qr-${shortCode}.png`;
    a.click();
  }

  function handlePrint() {
    window.print();
  }

  return (
    <>
      <style>
        {`
          @media print {
            body * {
              visibility: hidden;
            }
            #qr-print-area,
            #qr-print-area * {
              visibility: visible;
            }
            #qr-print-area {
              position: absolute;
              left: 50%;
              top: 50%;
              transform: translate(-50%, -50%);
              text-align: center;
            }
          }
        `}
      </style>

      <Card className="mb-6">
        <CardContent className="pt-6">
          <div id="qr-print-area" className="flex flex-col items-center gap-4">
            <div
              ref={qrRef}
              aria-label="QR code for walk-up waitlist"
              className="bg-white p-4 rounded-md"
            >
              <QRCodeCanvas
                value={shortUrl}
                size={240}
                className="w-[200px] h-[200px] md:w-[240px] md:h-[240px]"
                level="M"
              />
            </div>

            <div className="text-center">
              <p className="font-mono text-sm text-muted-foreground break-all">{shortUrl}</p>
              <p className="text-xs text-muted-foreground mt-2">
                Scan to join the walk-up waitlist
              </p>
              <p className="text-xs text-muted-foreground print:block hidden mt-1">
                {new Date().toLocaleDateString()}
              </p>
            </div>

            <div className="flex gap-2 print:hidden">
              <Button variant="outline" size="sm" onClick={handleDownload}>
                <Download className="h-4 w-4 mr-2" />
                Download PNG
              </Button>
              <Button variant="outline" size="sm" onClick={handlePrint}>
                <Printer className="h-4 w-4 mr-2" />
                Print
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </>
  );
}
