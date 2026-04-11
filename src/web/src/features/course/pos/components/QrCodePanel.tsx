import { useRef, useState } from 'react';
import { QRCodeCanvas } from 'qrcode.react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Check, Copy, Download, Printer } from 'lucide-react';

interface QrCodePanelProps {
  shortCode: string;
}

export function QrCodePanel({ shortCode }: QrCodePanelProps) {
  const qrRef = useRef<HTMLDivElement>(null);
  const shortUrl = `${window.location.origin}/w/${shortCode}`;
  const [copied, setCopied] = useState(false);

  function handleCopy() {
    void navigator.clipboard.writeText(shortUrl).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

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
              className="p-4 rounded-md"
              style={{ backgroundColor: '#ffffff' }}
            >
              <QRCodeCanvas
                value={shortUrl}
                size={240}
                className="w-[200px] h-[200px] md:w-[240px] md:h-[240px]"
                level="M"
              />
            </div>

            <div className="text-center">
              <div className="flex items-center justify-center gap-2 print:block">
                <p className="font-mono text-sm text-ink-muted break-all">{shortUrl}</p>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 shrink-0 print:hidden"
                  onClick={handleCopy}
                  aria-label={copied ? 'Copied' : 'Copy join link'}
                >
                  {copied ? (
                    <Check className="h-4 w-4 text-green" />
                  ) : (
                    <Copy className="h-4 w-4" />
                  )}
                </Button>
              </div>
              <p className="text-xs text-ink-muted mt-2">
                Scan to join the walk-up waitlist
              </p>
              <p className="text-xs text-ink-muted print:block hidden mt-1">
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
