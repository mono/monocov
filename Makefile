include config.make

PROJECTNAME = monocov
GUI = gtk
LIBS=-r:Mono.Cecil
CFLAGS=-O2 -g

all: monocov.exe libmono-profiler-monocov.so symbols.exe

ifeq ($(GUI), gtk)
GUI_SRCS = \
	gui/gtk/MonoCov.cs \
	gui/gtk/CoverageView.cs \
	gui/gtk/SourceWindow.cs
GUI_LIBS = -pkg:gtk-sharp-2.0 -pkg:glade-sharp-2.0 -r:System.Drawing -resource:gui/gtk/monocov.glade,monocov.glade
GUI_DEPS=gui/gtk/monocov.glade
else
GUI_SRCS = \
	gui/qt/MonoCov.cs \
	gui/qt/CoverageView.cs \
	gui/qt/SourceWindow.cs \
	gui/qt/FilterDialog.cs 
GUI_LIBS = -r Qt
endif

SRCS = \
	Constants.cs \
	CoverageItem.cs \
	NamespaceCoverageItem.cs \
	ClassCoverageItem.cs \
	MethodCoverageItem.cs \
	CoverageModel.cs \
	SourceFileCoverageData.cs \
	XmlExporter.cs \
	HtmlExporter.cs \
	MonoCovMain.cs \
	$(GUI_SRCS)

monocov.exe: $(SRCS) style.xsl .gui-$(GUI) $(GUI_DEPS)
	gmcs -debug /target:exe /out:$@ -define:GUI_$(GUI) $(LIBS) -r:Mono.CompilerServices.SymbolWriter -r:Mono.GetOptions $(GUI_LIBS) $(SRCS) -resource:style.xsl,style.xsl -resource:trans.gif,trans.gif

.gui-gtk:
	@rm -f .gui-*
	@touch .gui-gtk

.gui-qt:
	@rm -f .gui-*
	@touch .gui-qt

symbols.exe: symbols.cs
	gmcs -debug /target:exe /out:$@ -r:Mono.CompilerServices.SymbolWriter symbols.cs

nunit-console.exe: nunit-console.cs
	gmcs -r:nunit.framework -r:nunit.core -r:nunit.util -r:Mono.GetOptions nunit-console.cs

libmono-profiler-monocov.so: coverage.c
	$(CC) $(CFLAGS) -DVERSION=\"$(VERSION)\" `pkg-config --cflags mono` --shared -fPIC -o $@ $^

install: all
	mkdir -p $(prefix)/lib/monocov
	mkdir -p $(prefix)/man/man1
	cp Mono.Cecil.dll $(prefix)/lib/monocov
	cp monocov.exe $(prefix)/lib/monocov
	cp monocov $(prefix)/bin
	cp libmono-profiler-monocov.so $(prefix)/lib/
	cp monocov.1 $(prefix)/man/man1

test:
	gmcs -debug test.cs
	mono --profile=monocov:outfile=res.cov test.exe

cortests:
	MONO_PATH=../mcs/class/corlib mono --profile=monocov:outfile=corlib-tests.cov,+[mscorlib] nunit-console.exe corlib_test.dll

xml-cortests:
	mono ./monocov.exe --export-xml=export corlib-tests.cov
	tar cvzf corlib-tests.tar.gz export

html-cortests:
	mono ./monocov.exe --export-html=html-export corlib-tests.cov
	tar cvzf html-tests.tar.gz html-export

emittests:
	MONO_PATH=../mcs/class/corlib/Test mono --profile=monocov:outfile=emittests.cov,+[corlib]System.Reflection.Emit nunit-console.exe corlib_test.dll Reflection.Emit

hash-test:
	mono --profile=monocov:+Hashtable hash-table.exe

test-colorizer.exe: test-colorizer.cs SyntaxHighlighter.cs
	gmcs -debug /out:$@ $^

clean:
	rm -f monocov.exe monocov.exe.mdb symbols.exe symbols.exe.mdb nunit-console.exe libmono-profiler-monocov.so

distclean:
	rm -f monocov config.make Constants.cs

dist:
	tar -chzf $(PROJECTNAME)-$(VERSION).tar.gz `cat MANIFEST` \
		&& DIRNAME=$(PROJECTNAME)-$(VERSION) && rm -rf $$DIRNAME \
		&& mkdir $$DIRNAME && mv $(PROJECTNAME)-$(VERSION).tar.gz $$DIRNAME \
		&& cd $$DIRNAME && tar -xzf $(PROJECTNAME)-$(VERSION).tar.gz \
		&& rm $(PROJECTNAME)-$(VERSION).tar.gz && cd - && tar -cvzf $$DIRNAME.tar.gz $$DIRNAME \
		&& rm -rf $$DIRNAME

distrib:
	tar -cvhzf $(PROJECTNAME).tar.gz `cat MANIFEST` && DIRNAME=$(PROJECTNAME)-`date +%d-%b-%y` && rm -rf $$DIRNAME && mkdir $$DIRNAME && mv $(PROJECTNAME).tar.gz $$DIRNAME && cd $$DIRNAME && tar -xzf $(PROJECTNAME).tar.gz && rm $(PROJECTNAME).tar.gz && cd - && tar -cvzf $$DIRNAME.tar.gz $$DIRNAME && rm -rf $$DIRNAME
