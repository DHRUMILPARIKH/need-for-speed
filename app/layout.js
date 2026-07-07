import './globals.css';

export const metadata = {
  title: 'Apex Rush',
  description: 'Arcade street racing in the browser',
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
