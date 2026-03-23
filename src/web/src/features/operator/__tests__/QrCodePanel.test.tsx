import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import { QrCodePanel } from '../components/QrCodePanel';

// Mock QRCodeCanvas component
vi.mock('qrcode.react', () => ({
  QRCodeCanvas: ({ value }: { value: string }) => (
    <canvas data-testid="qr-canvas" data-value={value} />
  ),
}));

describe('QrCodePanel', () => {
  const mockShortCode = '4827';
  const mockOrigin = 'http://localhost:3000';
  let originalLocation: Location;

  beforeEach(() => {
    originalLocation = window.location;
    delete (window as { location?: Location }).location;
    window.location = { ...originalLocation, origin: mockOrigin } as Location;
  });

  afterEach(() => {
    window.location = originalLocation;
  });

  it('renders QR code canvas with correct URL', () => {
    render(<QrCodePanel shortCode={mockShortCode} />);

    const canvas = screen.getByTestId('qr-canvas');
    expect(canvas).toBeInTheDocument();
    expect(canvas).toHaveAttribute('data-value', `${mockOrigin}/w/${mockShortCode}`);
  });

  it('displays short URL text', () => {
    render(<QrCodePanel shortCode={mockShortCode} />);

    expect(screen.getByText(`${mockOrigin}/w/${mockShortCode}`)).toBeInTheDocument();
  });

  it('shows download and print buttons', () => {
    render(<QrCodePanel shortCode={mockShortCode} />);

    expect(screen.getByRole('button', { name: /download png/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /print/i })).toBeInTheDocument();
  });

  it('has aria-label on QR code wrapper', () => {
    render(<QrCodePanel shortCode={mockShortCode} />);

    expect(screen.getByLabelText('QR code for walk-up waitlist')).toBeInTheDocument();
  });

  it('calls window.print when print button is clicked', () => {
    const printSpy = vi.spyOn(window, 'print').mockImplementation(() => {});

    render(<QrCodePanel shortCode={mockShortCode} />);

    fireEvent.click(screen.getByRole('button', { name: /print/i }));

    expect(printSpy).toHaveBeenCalledOnce();

    printSpy.mockRestore();
  });

  it('triggers download when download button is clicked', () => {
    // Mock canvas.toDataURL
    const mockDataUrl = 'data:image/png;base64,mockdata';
    HTMLCanvasElement.prototype.toDataURL = vi.fn(() => mockDataUrl);

    // Mock anchor click
    const mockClick = vi.fn();
    const originalCreateElement = document.createElement.bind(document);
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((tagName) => {
      if (tagName === 'a') {
        return {
          href: '',
          download: '',
          click: mockClick,
        } as unknown as HTMLElement;
      }
      return originalCreateElement(tagName);
    });

    render(<QrCodePanel shortCode={mockShortCode} />);

    fireEvent.click(screen.getByRole('button', { name: /download png/i }));

    expect(mockClick).toHaveBeenCalledOnce();
    expect(HTMLCanvasElement.prototype.toDataURL).toHaveBeenCalledWith('image/png');

    createElementSpy.mockRestore();
  });
});
