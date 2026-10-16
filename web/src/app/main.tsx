import React from 'react';
import ReactDOM from 'react-dom/client';
import '../../../design/tokens.css';
import './fonts.css';
import './global.css';
import './legacy.css';
import { App } from './App';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
