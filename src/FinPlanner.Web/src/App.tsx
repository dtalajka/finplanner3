import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

type Account = { id: string; name: string; currency: string; type: string; isActive: boolean }
type Transaction = { id: string; familyAccountId: string; accountName: string; description: string; amount: number; type: 'Income' | 'Expense'; transactionDate: string }
type TransactionForm = { familyAccountId: string; description: string; amount: string; type: 'Income' | 'Expense'; transactionDate: string }

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5140/api'
const emptyForm: TransactionForm = { familyAccountId: '', description: '', amount: '', type: 'Expense', transactionDate: new Date().toISOString().slice(0, 10) }

function periodBounds(period: string) {
  const [year, month] = period.split('-').map(Number)
  const lastDay = new Date(year, month, 0).getDate()
  return { from: `${period}-01`, to: `${period}-${String(lastDay).padStart(2, '0')}` }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, { headers: { 'Content-Type': 'application/json' }, ...options })
  if (!response.ok) throw new Error(await response.text() || `Request failed (${response.status})`)
  return response.status === 204 ? (undefined as T) : response.json()
}

function App() {
  const [transactions, setTransactions] = useState<Transaction[]>([])
  const [accounts, setAccounts] = useState<Account[]>([])
  const [activeView, setActiveView] = useState('Transactions')
  const [showComposer, setShowComposer] = useState(false)
  const [showAccountComposer, setShowAccountComposer] = useState(false)
  const [accountName, setAccountName] = useState('')
  const [accountType, setAccountType] = useState('Checking')
  const [accountCurrency, setAccountCurrency] = useState('EUR')
  const [editingAccountId, setEditingAccountId] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<TransactionForm>(emptyForm)
  const [period, setPeriod] = useState(() => new Date().toISOString().slice(0, 7))
  const [typeFilter, setTypeFilter] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const loadData = async () => {
    setLoading(true)
    setError('')
    try {
      const [loadedAccounts, loadedTransactions] = await Promise.all([
        request<Account[]>(`/accounts?includeInactive=${activeView === 'Accounts'}`),
        request<Transaction[]>(`/transactions?from=${periodBounds(period).from}&to=${periodBounds(period).to}${typeFilter ? `&type=${typeFilter}` : ''}`),
      ])
      setAccounts(loadedAccounts)
      setTransactions(loadedTransactions)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not load data.')
    } finally { setLoading(false) }
  }

  useEffect(() => { void loadData() }, [period, typeFilter, activeView])

  const openCreate = () => { setEditingId(null); setForm({ ...emptyForm, familyAccountId: accounts[0]?.id ?? '' }); setShowComposer(true) }
  const openEdit = (transaction: Transaction) => { setEditingId(transaction.id); setForm({ familyAccountId: transaction.familyAccountId, description: transaction.description, amount: String(transaction.amount), type: transaction.type, transactionDate: transaction.transactionDate }); setShowComposer(true) }
  const closeComposer = () => { if (!saving) setShowComposer(false) }
  const createAccount = async (event: FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    try {
      const payload = { name: accountName, type: accountType, currency: accountCurrency, isActive: true }
      await request<Account>(editingAccountId ? `/accounts/${editingAccountId}` : '/accounts', { method: editingAccountId ? 'PUT' : 'POST', body: JSON.stringify(payload) })
      setAccountName(''); setEditingAccountId(null); setShowAccountComposer(false); await loadData()
    } catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Could not create account.') } finally { setSaving(false) }
  }

  const openAccountCreate = () => { setEditingAccountId(null); setAccountName(''); setAccountType('Checking'); setAccountCurrency('EUR'); setShowAccountComposer(true) }
  const openAccountEdit = (account: Account) => { setEditingAccountId(account.id); setAccountName(account.name); setAccountType(account.type); setAccountCurrency(account.currency); setShowAccountComposer(true) }
  const deactivateAccount = async (account: Account) => {
    if (!window.confirm(`Deactivate “${account.name}”? Existing transactions will be preserved.`)) return
    try { await request(`/accounts/${account.id}`, { method: 'DELETE' }); await loadData() }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Could not deactivate account.') }
  }

  const saveTransaction = async (event: FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    try {
      const payload = { ...form, amount: Number(form.amount) }
      await request<Transaction>(editingId ? `/transactions/${editingId}` : '/transactions', { method: editingId ? 'PUT' : 'POST', body: JSON.stringify(payload) })
      setShowComposer(false); await loadData()
    } catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Could not save transaction.') } finally { setSaving(false) }
  }

  const deleteTransaction = async (transaction: Transaction) => {
    if (!window.confirm(`Delete “${transaction.description}”?`)) return
    try { await request(`/transactions/${transaction.id}`, { method: 'DELETE' }); await loadData() }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Could not delete transaction.') }
  }

  const income = transactions.filter(item => item.type === 'Income').reduce((total, item) => total + item.amount, 0)
  const expenses = transactions.filter(item => item.type === 'Expense').reduce((total, item) => total + item.amount, 0)
  const euro = new Intl.NumberFormat('en-IE', { style: 'currency', currency: accounts[0]?.currency ?? 'EUR' })

  return <div className={`app-shell ${activeView.toLowerCase()}`}>
    <aside className="sidebar"><div className="brand"><span className="brand-mark">F</span><span>finplanner</span></div><div className="family-switcher"><span className="avatar">T</span><span><strong>Talajka family</strong><small>Household plan</small></span></div><nav aria-label="Main navigation">{['Overview', 'Transactions', 'Budgets', 'Accounts'].map(item => <button className={activeView === item ? 'nav-item active' : 'nav-item'} key={item} onClick={() => setActiveView(item)} type="button"><span className="nav-icon">{item === 'Overview' ? '⌂' : item === 'Transactions' ? '↕' : item === 'Budgets' ? '◒' : '◫'}</span>{item}</button>)}</nav><div className="sidebar-bottom"><div className="profile"><span className="avatar small">D</span><span><strong>Dušan Talajka</strong><small>Admin</small></span></div></div></aside>
    <main className="main-content"><header className="topbar"><div className="breadcrumb">Workspace <span>/</span> {activeView}</div><div className="top-actions">{activeView === 'Accounts' ? <button className="add-button" onClick={openAccountCreate} type="button"><span>+</span> Add account</button> : accounts.length ? <button className="add-button" onClick={openCreate} type="button"><span>+</span> Add transaction</button> : <button className="add-button" onClick={openAccountCreate} type="button"><span>+</span> Create account</button>}</div></header>
      <div className="content-wrap"><section className="page-heading"><div><p className="eyebrow">Household activity</p><h1>{activeView === 'Accounts' ? 'Accounts' : 'Transactions'}</h1><p className="muted">{activeView === 'Accounts' ? 'Manage the accounts used by your household.' : 'Track every income and expense across your family accounts.'}</p></div>{activeView !== 'Accounts' && <button className="period-button" type="button">{period} <span>⌄</span></button>}</section>
        {error && <div className="error-banner" role="alert">{error}</div>}
        <section className="metric-grid"><article className="metric-card"><div className="metric-top"><span>Income this month</span><span className="metric-icon income">↗</span></div><strong>{euro.format(income)}</strong></article><article className="metric-card"><div className="metric-top"><span>Expenses this month</span><span className="metric-icon expense">↘</span></div><strong>{euro.format(expenses)}</strong></article><article className="metric-card"><div className="metric-top"><span>Net this month</span><span className="metric-icon savings">✦</span></div><strong>{euro.format(income - expenses)}</strong></article><article className="metric-card"><div className="metric-top"><span>Transactions</span><span className="metric-icon">#</span></div><strong>{transactions.length}</strong></article></section>
        <section className="panel transaction-panel"><div className="panel-heading"><div><h2>All transactions</h2><p className="muted">{period} · {transactions.length} records</p></div><div className="transaction-filters"><input aria-label="Month" type="month" value={period} onChange={event => setPeriod(event.target.value)} /><select aria-label="Transaction type" value={typeFilter} onChange={event => setTypeFilter(event.target.value)}><option value="">All types</option><option value="Income">Income</option><option value="Expense">Expenses</option></select></div></div>{loading ? <p className="empty-state">Loading transactions...</p> : !transactions.length ? <div className="empty-state"><strong>No transactions yet</strong><span>{accounts.length ? 'Add your first income or expense to start tracking this month.' : 'Create your first account before adding transactions.'}</span><button className="add-button" onClick={() => accounts.length ? openCreate() : setShowAccountComposer(true)} type="button">+ {accounts.length ? 'Add transaction' : 'Create account'}</button></div> : <div className="transaction-list">{transactions.map(transaction => <div className="transaction" key={transaction.id}><span className={`transaction-icon ${transaction.type === 'Income' ? 'yellow' : 'green'}`}>{transaction.description.charAt(0).toUpperCase()}</span><span className="transaction-info"><strong>{transaction.description}</strong><small>{transaction.accountName} · {transaction.transactionDate}</small></span><strong className={transaction.type === 'Income' ? 'amount income-amount' : 'amount'}>{transaction.type === 'Income' ? '+' : '-'}{euro.format(transaction.amount)}</strong><button className="row-action" onClick={() => openEdit(transaction)} aria-label={`Edit ${transaction.description}`} type="button">✎</button><button className="row-action danger" onClick={() => void deleteTransaction(transaction)} aria-label={`Delete ${transaction.description}`} type="button">×</button></div>)}</div>}</section>
        <section className="panel account-panel" id="accounts"><div className="panel-heading"><div><h2>Family accounts</h2><p className="muted">{accounts.length} active or inactive accounts</p></div><button className="link-button" onClick={openAccountCreate} type="button">Add account <span>+</span></button></div><div className="account-list">{accounts.map(account => <div className="account-row" key={account.id}><span className="account-badge">{account.name.charAt(0).toUpperCase()}</span><span className="transaction-info"><strong>{account.name}</strong><small>{account.type} · {account.currency} · {account.isActive ? 'Active' : 'Inactive'}</small></span><button className="row-action" onClick={() => openAccountEdit(account)} aria-label={`Edit ${account.name}`} type="button">✎</button>{account.isActive && <button className="row-action danger" onClick={() => void deactivateAccount(account)} aria-label={`Deactivate ${account.name}`} type="button">×</button>}</div>)}</div></section>
      </div></main>
    {showComposer && <div className="modal-backdrop" role="presentation" onClick={closeComposer}><form className="modal" onSubmit={saveTransaction} onClick={event => event.stopPropagation()}><div className="modal-heading"><div><p className="eyebrow">Household activity</p><h2>{editingId ? 'Edit transaction' : 'Add transaction'}</h2></div><button className="close-button" aria-label="Close" onClick={closeComposer} type="button">×</button></div><label>Description<input required maxLength={200} value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} placeholder="e.g. Weekly groceries" /></label><div className="form-row"><label>Amount<input required min="0.01" step="0.01" type="number" value={form.amount} onChange={event => setForm({ ...form, amount: event.target.value })} placeholder="0.00" /></label><label>Type<select value={form.type} onChange={event => setForm({ ...form, type: event.target.value as TransactionForm['type'] })}><option value="Expense">Expense</option><option value="Income">Income</option></select></label></div><div className="form-row"><label>Date<input required type="date" value={form.transactionDate} onChange={event => setForm({ ...form, transactionDate: event.target.value })} /></label><label>Account<select required value={form.familyAccountId} onChange={event => setForm({ ...form, familyAccountId: event.target.value })}>{accounts.map(account => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label></div><button className="add-button full" disabled={saving || !accounts.length} type="submit">{saving ? 'Saving...' : 'Save transaction'}</button></form></div>}
    {showAccountComposer && <div className="modal-backdrop" role="presentation" onClick={() => !saving && setShowAccountComposer(false)}><form className="modal" onSubmit={createAccount} onClick={event => event.stopPropagation()}><div className="modal-heading"><div><p className="eyebrow">Account management</p><h2>{editingAccountId ? 'Edit account' : 'Create account'}</h2></div><button className="close-button" aria-label="Close" onClick={() => setShowAccountComposer(false)} type="button">×</button></div><label>Account name<input required maxLength={120} value={accountName} onChange={event => setAccountName(event.target.value)} placeholder="e.g. Main checking" /></label><div className="form-row"><label>Type<select value={accountType} onChange={event => setAccountType(event.target.value)}><option value="Checking">Checking</option><option value="Savings">Savings</option><option value="Cash">Cash</option><option value="CreditCard">Credit card</option><option value="Investment">Investment</option><option value="Loan">Loan</option><option value="Other">Other</option></select></label><label>Currency<input required minLength={3} maxLength={3} value={accountCurrency} onChange={event => setAccountCurrency(event.target.value.toUpperCase())} /></label></div><p className="muted">Deactivating an account preserves its existing transactions.</p><button className="add-button full" disabled={saving} type="submit">{saving ? 'Saving...' : editingAccountId ? 'Save account' : 'Create account'}</button></form></div>}
  </div>
}

export default App
