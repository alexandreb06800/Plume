// Alias globaux vers les types WPF. Hérités de l'époque WinForms (ambiguïté disparue avec
// UseWindowsForms, retiré en 5.2.0) ; conservés car largement utilisés dans le code.
global using Application = System.Windows.Application;
global using Clipboard = System.Windows.Clipboard;
global using MessageBox = System.Windows.MessageBox;
global using DataObject = System.Windows.DataObject;
global using DataFormats = System.Windows.DataFormats;
