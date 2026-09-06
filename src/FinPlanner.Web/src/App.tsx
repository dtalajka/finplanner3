import { useState } from 'react'
import './App.css'

type Transaction = { merchant: string; category: string; date: string; amount: string; tone: string }

const transactions: Transaction[] = [
  { merchant: 'Mestský market', category: 'Groceries', date: 'Today, 09:42', amount: '-€64.20', tone: 'green' },
  { merchant: 'Northline Energy', category: 'Utilities', date: 'Yesterday', amount: '-€118.00', tone: 'blue' },
  { merchant: 'Salary · September', category: 'Income', date: 'Sep 01, 08:10', amount: '+€2,840.00', tone: 'yellow' },
  { merchant: 'Little Pines School', category: 'Family', date: 'Aug 29', amount: '-€240.00', tone: 'purple' },
]

function App() {
  const [activeView, setActiveView] = useState('Overview')
  const [showComposer, setShowComposer] = useState(false)

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand"><span className="brand-mark">F</span><span>finplanner</span></div>
        <div className="family-switcher"><span className="avatar">T</span><span><strong>Talajka family</strong><small>Household plan</small></span><span className="chevron">⌄</span></div>
        <nav aria-label="Main navigation">
          {['Overview', 'Transactions', 'Budgets', 'Accounts'].map((item) => (
            <button className={activeView === item ? 'nav-item active' : 'nav-item'} key={item} onClick={() => setActiveView(item)} type="button">
              <span className="nav-icon">{item === 'Overview' ? '⌂' : item === 'Transactions' ? '↕' : item === 'Budgets' ? '◒' : '◫'}</span>{item}
            </button>
          ))}
        </nav>
        <div className="sidebar-bottom"><button className="nav-item" type="button"><span className="nav-icon">⚙</span>Settings</button><div className="profile"><span className="avatar small">D</span><span><strong>Dušan Talajka</strong><small>Admin</small></span><span className="dots">···</span></div></div>
      </aside>

      <main className="main-content">
        <header className="topbar"><div className="breadcrumb">Workspace <span>/</span> {activeView}</div><div className="top-actions"><button className="icon-button" aria-label="Notifications" type="button">♧</button><button className="help-button" type="button">?</button><button className="add-button" onClick={() => setShowComposer(true)} type="button"><span>+</span> Add transaction</button></div></header>
        <div className="content-wrap">
          <section className="page-heading"><div><p className="eyebrow">Sunday, September 6, 2026</p><h1>Good morning, Dušan</h1><p className="muted">Here is how your household is doing this month.</p></div><button className="period-button" type="button">September 2026 <span>⌄</span></button></section>

          <section className="metric-grid" aria-label="Financial summary">
            <article className="metric-card balance"><div className="metric-top"><span>Current balance</span><span className="metric-icon">↗</span></div><strong>€8,420.50</strong><p className="positive">↑ 12.8% <span>vs last month</span></p><div className="sparkline"><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i></div></article>
            <article className="metric-card"><div className="metric-top"><span>Income</span><span className="metric-icon income">↗</span></div><strong>€2,840.00</strong><p className="positive">↑ 4.2% <span>vs last month</span></p><div className="bar-line"><i style={{ width: '74%' }}></i></div></article>
            <article className="metric-card"><div className="metric-top"><span>Expenses</span><span className="metric-icon expense">↘</span></div><strong>€1,248.70</strong><p className="negative">↓ 8.4% <span>vs last month</span></p><div className="bar-line orange"><i style={{ width: '43%' }}></i></div></article>
            <article className="metric-card"><div className="metric-top"><span>Savings rate</span><span className="metric-icon savings">✦</span></div><strong>56.0%</strong><p className="positive">↑ 6.1% <span>vs last month</span></p><div className="bar-line green"><i style={{ width: '56%' }}></i></div></article>
          </section>

          <section className="dashboard-grid"><article className="panel transactions"><div className="panel-heading"><div><h2>Recent transactions</h2><p className="muted">Your latest household activity</p></div><button className="link-button" type="button" onClick={() => setActiveView('Transactions')}>View all <span>→</span></button></div><div className="transaction-list">{transactions.map((transaction) => <div className="transaction" key={`${transaction.merchant}-${transaction.date}`}><span className={`transaction-icon ${transaction.tone}`}>{transaction.merchant.charAt(0)}</span><span className="transaction-info"><strong>{transaction.merchant}</strong><small>{transaction.category} · {transaction.date}</small></span><strong className={transaction.amount.startsWith('+') ? 'amount income-amount' : 'amount'}>{transaction.amount}</strong></div>)}</div></article><article className="panel budget"><div className="panel-heading"><div><h2>Monthly budget</h2><p className="muted">September overview</p></div><button className="more-button" type="button" aria-label="More budget options">···</button></div><div className="budget-total"><strong>€1,248.70</strong><span>of €2,400</span></div><div className="budget-track"><i></i></div><div className="budget-meta"><span>52% used</span><span>€1,151.30 left</span></div><div className="budget-categories"><div><span className="dot food"></span><span>Food & groceries</span><strong>€420</strong></div><div><span className="dot home"></span><span>Home & utilities</span><strong>€310</strong></div><div><span className="dot family"></span><span>Family</span><strong>€240</strong></div></div></article></section>
        </div>
      </main>

      {showComposer && <div className="modal-backdrop" role="presentation" onClick={() => setShowComposer(false)}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title" onClick={(event) => event.stopPropagation()}><div className="modal-heading"><div><p className="eyebrow">Household activity</p><h2 id="modal-title">Add transaction</h2></div><button className="close-button" aria-label="Close" onClick={() => setShowComposer(false)} type="button">×</button></div><label>What was this for?<input placeholder="e.g. Weekly groceries" /></label><div className="form-row"><label>Amount<input placeholder="€ 0.00" inputMode="decimal" /></label><label>Type<select defaultValue="expense"><option value="expense">Expense</option><option value="income">Income</option></select></label></div><button className="add-button full" type="button" onClick={() => setShowComposer(false)}>Save transaction</button></div></div>}
    </div>
  )
}

export default App
