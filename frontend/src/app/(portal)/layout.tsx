import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'SmartHotel Guest Portal',
  description: 'Manage your hotel booking, request services, and track your stay.',
}

export default function PortalLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <div className="min-h-screen" style={{ background: 'linear-gradient(135deg, #0f0c29, #302b63, #24243e)' }}>
      {/* Portal Header */}
      <header className="border-b" style={{ borderColor: 'rgba(226,185,111,0.2)', background: 'rgba(15,12,41,0.8)', backdropFilter: 'blur(12px)' }}>
        <div className="max-w-5xl mx-auto px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)' }}>
              <span className="text-sm font-bold" style={{ color: '#1a1a2e' }}>S</span>
            </div>
            <div>
              <span className="font-bold text-white tracking-wider text-sm">SMARTHOTEL</span>
              <span className="ml-2 text-xs px-2 py-0.5 rounded-full" style={{ background: 'rgba(226,185,111,0.15)', color: '#e2b96f', border: '1px solid rgba(226,185,111,0.3)' }}>
                Guest Portal
              </span>
            </div>
          </div>
          <p className="text-xs" style={{ color: '#718096' }}>Secure Guest Access</p>
        </div>
      </header>

      {/* Main content */}
      <main className="max-w-5xl mx-auto px-6 py-8">
        {children}
      </main>

      {/* Footer */}
      <footer className="text-center py-6" style={{ color: '#4a5568', fontSize: '12px' }}>
        © {new Date().getFullYear()} SmartHotel. Your data is encrypted and secure.
      </footer>
    </div>
  )
}
